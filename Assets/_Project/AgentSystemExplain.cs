/*
[Header("Movement")]
public float attractionStrength = 0.35f;   // 社会关系总引力强度：越大越容易抱团；越小越分散
public float moveSpeed = 0.001f;           // 实际移动速度：越大走得越快；越小越慢
public float velocityDamping = 0.92f;      // 速度阻尼：越大越容易“滑行”；越小越快停下

[Header("Bounds")]
public float cameraPadding = 0.25f;        // 离屏幕边缘的安全距离：越大越不贴边；越小越靠边

[Header("Behavior Movement Effects")]
public float loveForceBonus = 0.12f;       // Love 额外吸引：越大越容易情侣/朋友抱死团；越小越自然
public float silenceForceMultiplier = 0.35f; // Silence 时保留多少移动：越大沉默者还会动；越小越像静止
public float violenceForceBonus = 0.45f;   // Violence 额外冲向别人：越大越容易追杀；越小冲突更弱

[Header("Food Seeking")]
public float lowFoodSeekThreshold = 0.8f;  // 食物低于多少开始主动找吃的：越大越早去觅食；越小越拖到饿坏才找
public float foodSeekForce = 6.0f;         // 朝食物移动的基础力量：越大越会“冲向食物”；越小更容易继续社交
public float hungrySeekBonus = 3.0f;       // 饥饿越高时额外觅食加成：越大越像求生本能；越小差别不明显

[Header("Food Respawn")]
public float foodRespawnInterval = 5f;     // 食物刷新间隔：越大资源越稀缺；越小世界越富裕
public int foodRespawnAmount = 1;          // 每次刷几个食物：越大越不容易饿死；越小更容易发生争抢
public float foodSpawnPadding = 0.5f;      // 食物离屏幕边缘距离：越大越往中间刷；越小越靠边刷

[Header("Relation Change")]
public float tickInterval = 0.1f;          // 单步模拟时间：通常不要手改，由 SimulationEngine 控
public float friendDistance = 1.2f;        // 多近才容易变朋友：越大越容易交朋友；越小更难建立社会关系
public float forgetDistance = 3.5f;        // 多远开始“疏远”：越小关系更容易散；越大关系更稳
public float befriendTime = 4.0f;          // 变朋友需要多久：越大越难交朋友；越小很快全员朋友
public float forgetTime = 3.0f;            // 忘记朋友需要多久：越大关系更持久；越小更流动

[Header("Love Bond")]
public float loveBondDistance = 0.8f;      // Love 状态下要多近才强化关系：越小更亲密才行；越大更容易绑定
public float loveBondTime = 2.0f;          // Love 维持多久才强化关系：越大更难“相爱成友”；越小更快绑定

[Header("Birth")]
public float birthDistance = 0.5f;         // 生育时两者必须多近：越小更难生；越大更容易生
public float birthLoveTime = 4.0f;         // Love 持续多久才可能生：越大更稀少；越小容易爆人口
public float reproductionCooldownTime = 20.0f; // 生完后冷却：越大越抑制人口；越小更快再生
public float birthMinFoodRequired = 0.9f;  // 生育最低食物要求：越大越“富裕才生”；越小穷也能生
public float birthFoodCost = 0.45f;        // 生育消耗食物：越大生完更虚弱；越小生育代价轻

[Header("Condition Check Radius")]
public float lonelinessCheckRadius = 1.8f; // 多少范围内算“有人陪”：越大越不容易孤独；越小更容易孤独
public float joyfulCheckRadius = 0.8f;     // 多近才会愉悦：越小更强调亲密接触；越大容易快乐

[Header("Behavior Thresholds")]
public float violenceThreshold = 0.22f;    // Hungry 超过多少触发暴力：越小越容易打架；越大更克制
public float silenceThreshold = 0.65f;     // Lonely 超过多少沉默：越小更容易封闭；越大更外向
public float loveThreshold = 0.72f;        // Joyful 超过多少进入 Love：越小越容易恋爱；越大更少
public float hurtViolenceThreshold = 0.35f; // Hurt 超过多少触发报复：越小更容易复仇；越大更能忍

[Header("Combat")]
public float attackDistance = 0.45f;       // 攻击距离：越大更容易打到；越小必须贴脸
public float attackDamage = 0.08f;         // 单次伤害：越大死得快；越小冲突更持久

[Header("Food")]
public float foodDecayPerTick = 0.003f;    // 每步食物自然消耗：越大社会压力越大；越小更容易长期稳定
public float hungerFromLowFoodPerTick = 0.015f; // 缺粮时饥饿增长：越大更快进入抢食/暴力；越小缓慢
public float starvingHpLossPerTick = 0.006f; // 断粮时掉血：越大更快饿死；越小可拖更久
public float lowFoodThreshold = 0.45f;     // 低于多少算“缺粮”：越大越早进入资源危机；越小更晚

[Header("Food Pickup")]
public float foodPickupDistance = 0.5f;    // 多近能吃到食物：越大更容易吃到；越小必须精准靠近
public float hungryReliefOnEat = 0.28f;    // 吃一次降低多少饥饿：越大食物更有效；越小一顿不够
*/