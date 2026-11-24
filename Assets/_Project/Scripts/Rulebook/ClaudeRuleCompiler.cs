using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// "Speak to the World" — turns a natural-language wish into Rule Book edits.
///
/// The user types an intent ("make loneliness contagious", "let enemies fall
/// in love"); DeepSeek compiles it into the Rule Book's constrained grammar and
/// returns a strict JSON patch, which is validated against the legal card
/// values and written into RuleBookState. The LLM never touches the
/// simulation directly — it can only speak the same five sentences the
/// audience can, which is the whole point: fuzzy human intent, compiled into
/// an externalized, inspectable policy.
///
/// Key: put your DeepSeek API key in StreamingAssets/deepseek_key.txt
/// (gitignored). Desktop builds only — the key must never ship in a public
/// WebGL build, so web versions hide this feature.
/// </summary>
public class ClaudeRuleCompiler : MonoBehaviour
{
    public string model = "deepseek-flash";
    public int maxTokens = 400;

    public bool IsBusy { get; private set; }

    /// <summary>Developer key (repo-local, gitignored).</summary>
    public static string KeyPath => Path.Combine(Application.streamingAssetsPath, "deepseek_key.txt");

    /// <summary>End-user key, entered on the title screen. Lives in the OS
    /// per-user app-data folder — never inside the project, never in git,
    /// only ever sent to api.deepseek.com over HTTPS.</summary>
    public static string UserKeyPath => Path.Combine(Application.persistentDataPath, "deepseek_key.txt");

    public static bool HasStoredKey => !string.IsNullOrEmpty(LoadKey());

    /// <summary>Save (or, with an empty string, delete) the user's key.
    /// Returns a short human-readable status.</summary>
    public static string SaveUserKey(string key)
    {
        try
        {
            key = key != null ? key.Trim() : "";

            if (key.Length == 0)
            {
                if (File.Exists(UserKeyPath)) File.Delete(UserKeyPath);
                return "已从此设备移除密钥";
            }

            if (key.Length < 12)
                return "请输入有效的 API 密钥";

            File.WriteAllText(UserKeyPath, key);
            return "已保存到此设备";
        }
        catch (Exception e)
        {
            return "无法保存密钥（" + e.GetType().Name + "）";
        }
    }

    const string ApiUrl = "https://api.deepseek.com/anthropic/v1/messages";
    const string ApiVersion = "2023-06-01";

    WorldParameterRegistry registry;
    AgentSpawner spawner;

    void Awake()
    {
        spawner = FindFirstObjectByType<AgentSpawner>();
        registry = new WorldParameterRegistry(
            FindFirstObjectByType<AgentSystem>(),
            FindFirstObjectByType<AgentSystem_FoodHungry>(),
            FindFirstObjectByType<AgentSystem_MovementSystem>(),
            FindFirstObjectByType<SimulationEngine>());
    }

    const string SystemPromptHeader =
        "You are the voice of MYLIFE, a life-simulation of autonomous agents who eat, " +
        "love, fight, reproduce, found churches, and die. A human speaks to the world; " +
        "you answer its questions and reshape it toward what they describe.\n\n" +
        "You have TWO levers.\n\n" +
        "LEVER 1 — THE RULE BOOK, five laws with fillable blanks:\n" +
        "1. [rule1A] and [rule1B] draw together  — those two kinds attract each other\n" +
        "2. [rule2A] and [rule2B] turn away  — they repel each other\n" +
        "3. [becomesFrom] becomes [becomesTo]  — one perceived identity is seen as another\n" +
        "4. [spreadsCondition] spreads  — that condition becomes contagious nearby\n" +
        "5. when [rule5Identity] is [rule5Condition], [rule5Behavior] rises\n" +
        "Identity options: Child, Friend, Enemy, Other\n" +
        "Condition options: Hungry, Lonely, Joyful, Hurt, Dead\n" +
        "Behavior options: Love, Violence, Silence\n\n" +
        "LEVER 2 — THE PHYSICS, tunable parameters (current value, legal range, meaning):\n";

    const string SystemPromptContract =
        "\nReply with ONLY a JSON object, no code fences, shaped like:\n" +
        "{\"rule1A\":\"Friend\", ..., \"tune\":[{\"name\":\"attackDamage\",\"value\":0.05}], " +
        "\"answer\":\"...\"}\n" +
        "- Rule fields: include ONLY blanks you want to change; exact option words; " +
        "\"clear\" empties a blank.\n" +
        "- \"tune\": include ONLY parameters you want to change; exact names from the " +
        "list; values are clamped to the legal range.\n" +
        "- \"answer\": always required — one or two short sentences (lowercase, plain, " +
        "slightly poetic) answering the human and/or describing what you changed.\n" +
        "- Write the answer in Simplified Chinese. Keep JSON keys, option values, and parameter names exactly as specified.\n" +
        "- Pure questions need no rule/tune fields: just answer from the world state.\n" +
        "- Prefer the smallest change that fulfils the wish. If a wish cannot be " +
        "expressed with these levers, say so in the answer and change nothing.";

    // ---------------------------------------------------------------- API

    /// <summary>Compile a wish. Callback: (success, message) — message is
    /// DeepSeek's short comment on success, or an error description.</summary>
    public void Compile(string wish, Action<bool, string> done)
    {
        if (IsBusy) { done(false, "世界正在聆听……"); return; }

        string key = LoadKey();
        if (string.IsNullOrEmpty(key))
        {
            done(false, "请先在标题页保存 DeepSeek API 密钥");
            return;
        }

        StartCoroutine(CompileRoutine(wish, key, done));
    }

    static string LoadKey()
    {
        // User-entered key first, developer's StreamingAssets key as fallback.
        string key = ReadKeyFile(UserKeyPath);
        if (key == null) key = ReadKeyFile(KeyPath);
        return key;
    }

    static string ReadKeyFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            string key = File.ReadAllText(path).Trim();
            return key.Length > 10 && !key.Contains(" ") ? key : null;
        }
        catch
        {
            return null;
        }
    }

    IEnumerator CompileRoutine(string wish, string key, Action<bool, string> done)
    {
        string systemPrompt = SystemPromptHeader +
                              (registry != null ? registry.DescribeForPrompt() : "") +
                              SystemPromptContract;

        string userContent = "CURRENT WORLD STATE:\n" + BuildWorldContext() +
                             "\nTHE HUMAN SAYS:\n" + wish;

        WorldState requestWorld = spawner != null ? spawner.worldState : null;
        yield return SendRequest(systemPrompt, userContent, maxTokens, key,
            raw =>
            {
                if (spawner != null && spawner.worldState != requestWorld)
                {
                    done(false, "世界已重开，请重新发送这句话。");
                    return;
                }

                HandleResponse(raw, done);
            },
            error => done(false, error));
    }

    // ---------------------------------------------------- agent stories

    const string StorySystemPrompt =
        "You are a single agent inside MYLIFE, a small simulated world of autonomous " +
        "beings who wander, eat, befriend, love, fight, reproduce, found churches, and " +
        "die. You will be given your own current data — your age, your hunger and " +
        "loneliness, your bonds and kin, the laws currently governing your world.\n\n" +
        "Speak as that agent, first person, telling your own small life story so far. " +
        "Stay strictly grounded in the numbers and facts given — do not invent family " +
        "or events the data doesn't support, but you may give feelings to what the " +
        "numbers imply. Plain, quiet, slightly wistful prose. At most 110 words — " +
        "shorter is fine, but ALWAYS end on a complete sentence. A single unbroken " +
        "monologue, no headings, no lists, never mention numbers as numbers — " +
        "translate them into lived experience. Write the entire story in Simplified Chinese.";

    /// <summary>First-person life story for one agent, grounded in its data.</summary>
    public void TellAgentStory(string agentContext, Action<bool, string> done)
    {
        if (IsBusy) { done(false, "…"); return; }

        string key = LoadKey();
        if (string.IsNullOrEmpty(key))
        {
            done(false, "请先在标题页保存 DeepSeek API 密钥");
            return;
        }

        StartCoroutine(StoryRoutine(agentContext, key, done));
    }

    IEnumerator StoryRoutine(string agentContext, string key, Action<bool, string> done)
    {
        yield return SendRequest(StorySystemPrompt, agentContext, 300, key,
            raw =>
            {
                ApiResponse response = null;
                try { response = JsonUtility.FromJson<ApiResponse>(raw); } catch { }

                string text = response != null && response.content != null && response.content.Length > 0
                    ? response.content[0].text : null;

                if (string.IsNullOrEmpty(text)) done(false, "这个角色暂时没有故事可讲");
                else done(true, text.Trim());
            },
            error => done(false, error));
    }

    // ------------------------------------------------------- transport

    IEnumerator SendRequest(string systemPrompt, string userContent, int tokens, string key,
                            Action<string> onRaw, Action<string> onError)
    {
        IsBusy = true;

        var body = new RequestBody
        {
            model = model,
            max_tokens = tokens,
            thinking = new ThinkingOptions { type = "disabled" },
            system = systemPrompt,
            messages = new[] { new RequestMessage { role = "user", content = userContent } }
        };

        string json = JsonUtility.ToJson(body);

        using (UnityWebRequest request = new UnityWebRequest(ApiUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("content-type", "application/json");
            request.SetRequestHeader("x-api-key", key);
            request.SetRequestHeader("anthropic-version", ApiVersion);
            request.timeout = 30;

            yield return request.SendWebRequest();

            IsBusy = false;

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError(request.responseCode > 0
                    ? $"连接 DeepSeek 失败（HTTP {request.responseCode}）"
                    : "无法连接 DeepSeek，请检查网络或代理设置。");
                yield break;
            }

            onRaw(request.downloadHandler.text);
        }
    }

    void HandleResponse(string responseJson, Action<bool, string> done)
    {
        ApiResponse response;
        try
        {
            response = JsonUtility.FromJson<ApiResponse>(responseJson);
        }
        catch (Exception)
        {
            done(false, "无法读取世界的回复");
            return;
        }

        if (response?.content == null || response.content.Length == 0)
        {
            done(false, "世界没有返回内容");
            return;
        }

        string text = response.content[0].text ?? "";
        string patchJson = ExtractJsonObject(text);

        if (patchJson == null)
        {
            done(false, "无法读取世界的回复");
            return;
        }

        RulePatch patch;
        try
        {
            patch = JsonUtility.FromJson<RulePatch>(patchJson);
        }
        catch (Exception)
        {
            done(false, "无法读取世界的回复");
            return;
        }

        int applied = ApplyPatch(patch);
        applied += ApplyTuning(patch.tune);

        string answer = !string.IsNullOrEmpty(patch.answer) ? patch.answer
                      : !string.IsNullOrEmpty(patch.comment) ? patch.comment
                      : applied > 0 ? "规则已生效" : "世界没有理解这句话";

        done(applied > 0 || !string.IsNullOrEmpty(patch.answer) || !string.IsNullOrEmpty(patch.comment), answer);
    }

    int ApplyTuning(TuneEntry[] tune)
    {
        if (tune == null || registry == null) return 0;

        int applied = 0;
        for (int i = 0; i < tune.Length; i++)
        {
            if (tune[i] != null && !string.IsNullOrEmpty(tune[i].name) &&
                registry.TrySet(tune[i].name, tune[i].value, out float before, out float after) &&
                before != after)
            {
                spawner?.worldState?.EventLog.QueueParameterChange(tune[i].name, before, after);
                applied++;
            }
        }

        return applied;
    }

    /// <summary>Compact live snapshot the model reasons over: census, world
    /// objects, and the currently active rules.</summary>
    string BuildWorldContext()
    {
        if (spawner == null || spawner.worldState == null) return "(unknown)\n";

        WorldState world = spawner.worldState;
        SimulationStats stats = world.stats;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"world time: {world.worldTime:0}s");
        sb.AppendLine($"alive agents: {stats.agentsAlive} — in Love {stats.agentsInLove}, in Violence {stats.agentsInViolence}, in Silence {stats.agentsInSilence}");
        sb.AppendLine($"this tick: {stats.births} births, {stats.deaths} deaths, {stats.attacks} attacks, {stats.friendshipsFormed} friendships formed");
        sb.AppendLine($"world objects: {world.foods.Count} foods, {world.homes.Count} homes, {world.deathMarkers.Count} graves, {world.churches.Count} churches");
        sb.AppendLine("active rules: " + DescribeActiveRules());
        return sb.ToString();
    }

    public static string DescribeActiveRules()
    {
        var parts = new List<string>();

        if (RuleBookState.rule1A != "" && RuleBookState.rule1B != "")
            parts.Add($"{RuleBookState.rule1A} and {RuleBookState.rule1B} draw together");
        if (RuleBookState.rule2A != "" && RuleBookState.rule2B != "")
            parts.Add($"{RuleBookState.rule2A} and {RuleBookState.rule2B} turn away");
        if (RuleBookState.becomesFrom != "" && RuleBookState.becomesTo != "")
            parts.Add($"{RuleBookState.becomesFrom} becomes {RuleBookState.becomesTo}");
        if (RuleBookState.spreadsCondition != "")
            parts.Add($"{RuleBookState.spreadsCondition} spreads");
        if (RuleBookState.rule5Identity != "" && RuleBookState.rule5Condition != "" && RuleBookState.rule5Behavior != "")
            parts.Add($"when {RuleBookState.rule5Identity} is {RuleBookState.rule5Condition}, {RuleBookState.rule5Behavior} rises");

        return parts.Count > 0 ? string.Join("; ", parts) : "none";
    }

    static string ExtractJsonObject(string text)
    {
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        return text.Substring(start, end - start + 1);
    }

    // ------------------------------------------------------------ patching

    static readonly string[] Identities = { "Child", "Friend", "Enemy", "Other" };
    static readonly string[] Conditions = { "Hungry", "Lonely", "Joyful", "Hurt", "Dead" };
    static readonly string[] Behaviors = { "Love", "Violence", "Silence" };

    /// <summary>Validates every field against the legal card values and writes
    /// accepted ones into RuleBookState. Returns how many fields were applied.
    /// The model's output is treated as untrusted input throughout.</summary>
    static int ApplyPatch(RulePatch patch)
    {
        int applied = 0;

        applied += Apply(patch.rule1A, Identities, v => RuleBookState.rule1A = v);
        applied += Apply(patch.rule1B, Identities, v => RuleBookState.rule1B = v);
        applied += Apply(patch.rule2A, Identities, v => RuleBookState.rule2A = v);
        applied += Apply(patch.rule2B, Identities, v => RuleBookState.rule2B = v);
        applied += Apply(patch.becomesFrom, Identities, v => RuleBookState.becomesFrom = v);
        applied += Apply(patch.becomesTo, Identities, v => RuleBookState.becomesTo = v);
        applied += Apply(patch.spreadsCondition, Conditions, v => RuleBookState.spreadsCondition = v);
        applied += Apply(patch.rule5Identity, Identities, v => RuleBookState.rule5Identity = v);
        applied += Apply(patch.rule5Condition, Conditions, v => RuleBookState.rule5Condition = v);
        applied += Apply(patch.rule5Behavior, Behaviors, v => RuleBookState.rule5Behavior = v);

        return applied;
    }

    static int Apply(string value, string[] legal, Action<string> write)
    {
        if (string.IsNullOrEmpty(value)) return 0; // absent → untouched

        if (value.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            write("");
            return 1;
        }

        for (int i = 0; i < legal.Length; i++)
        {
            if (legal[i].Equals(value.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                write(legal[i]); // canonical casing
                return 1;
            }
        }

        return 0; // illegal value → ignored
    }

    // ------------------------------------------------- (de)serialization

#pragma warning disable 0649
    [Serializable]
    class RequestBody
    {
        public string model;
        public int max_tokens;
        public ThinkingOptions thinking;
        public string system;
        public RequestMessage[] messages;
    }

    [Serializable]
    class ThinkingOptions
    {
        public string type;
    }

    [Serializable]
    class RequestMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    class ApiResponse
    {
        public ContentBlock[] content;
    }

    [Serializable]
    class ContentBlock
    {
        public string type;
        public string text;
    }

    [Serializable]
    class RulePatch
    {
        public string rule1A;
        public string rule1B;
        public string rule2A;
        public string rule2B;
        public string becomesFrom;
        public string becomesTo;
        public string spreadsCondition;
        public string rule5Identity;
        public string rule5Condition;
        public string rule5Behavior;
        public TuneEntry[] tune;
        public string answer;
        public string comment; // legacy fallback
    }

    [Serializable]
    class TuneEntry
    {
        public string name;
        public float value;
    }
#pragma warning restore 0649
}
