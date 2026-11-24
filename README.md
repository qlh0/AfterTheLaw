# 法则之后 · After the Law

《法则之后》是一款基于 Unity 的 2D 社会模拟游戏。你观察一个小世界里的人们怎样生活、交友和发生冲突，也可以直接改写世界法则，再看这些变化如何影响他们。

本项目基于 Yichuan (River) Wang 的 [MYLIFE](https://github.com/RRiverWong/MYLIFE-Agents-simulation) 改造，保留了原版的自主模拟和规则手册，并加入简体中文界面、DeepSeek 对话和世界事件面板。

## 现在可以玩什么

- **观察角色**。开局有 16 位角色，默认人口上限为 20。点击人物可以查看年龄、食物、健康、情绪和当前行为。左侧按钮可以暂停或加速。
- **改写法则**。打开「规则手册」填入规则，或按 Tab 打开它。比如设定「孤独会传播」，然后观察附近角色的孤独值如何变化。绿线表示「相互靠近」命中，红线表示「相互远离」命中。
- **查看世界事件**。底部「世界事件」面板展示已生效的法则、稳定的关系变化、重要冲突和死亡。连续交手会合并显示，每局保留最近 200 条。
- **和世界说话**。接入 DeepSeek 后，可以用中文问世界的现状、提出法则改动，或从角色面板生成第一人称故事。Unity 根据实际世界状态运行模拟，并校验模型提出的规则和参数改动。

## 在 Windows 上启动

1. 安装 Unity Hub，并安装 **Unity 6.3 LTS `6000.3.9f1`**。建议使用项目配置里的这个版本。 [Unity 官方下载](https://unity.com/releases/editor/whats-new/6000.3.9f1)
2. 下载源码，或在终端克隆仓库。

   ```powershell
   git clone https://github.com/qlh0/AfterTheLaw.git
   ```

3. 在 Unity Hub 选择 **项目 → 添加 → 从磁盘添加项目**，选中包含 `Assets`、`Packages` 和 `ProjectSettings` 的项目根目录。首次打开时等待 Unity 导入资源和编译脚本。
4. 在 Unity 的 **Project** 面板打开 `Assets/_Project/Scenes/World.unity`，点顶部 **▶ Play**，再在 **Game** 窗口点 **开始模拟**。

基础模拟、手动规则手册和世界事件可以离线运行。首次导入时，Unity 可能需要联网下载项目依赖。

## 可选：启用 DeepSeek

1. 在 DeepSeek 开放平台创建 API Key。 [DeepSeek 开放平台](https://platform.deepseek.com/api_keys)
2. 进入 Play 模式后，在游戏标题页的 **DeepSeek API 密钥**输入框粘贴密钥，点击 **保存**。
3. 点击 **开始模拟 → 对话**，输入「现在世界里有多少人？」测试连接。选中角色后，也可以在「角色」面板点击「角色故事」。

密钥由游戏保存在本机用户数据目录，API 请求发往 DeepSeek，费用由你的 DeepSeek 账户承担。请通过游戏标题页保存密钥，避免把凭据写进源码或提交到 GitHub。

## 许可与致谢

原版 [MYLIFE](https://github.com/RRiverWong/MYLIFE-Agents-simulation) 由 Yichuan (River) Wang 创作。原项目的版权与 MIT 许可保留在 [LICENSE](LICENSE)。中文字体 Noto Sans CJK SC 使用 [SIL Open Font License](Assets/_Project/Resources/Fonts/OFL.txt)。
