# Windows Tool

本地服务与工具的**统一启停控制台**。视觉对标 [linusonn.com](https://linusonn.com)：
深空底色、星野粒子背景、青(#22D3EE)/紫(#A78BFA) 强调色、玻璃拟态卡片、无边框窗口。

```
✦ WINDOWS TOOL   本地服务控制台                    — ✕
─────────────────────────────────────────────────────
  1 运行中            全部启动  全部停止  刷新  配置文件
  / 8 项功能
─────────────────────────────────────────────────────
  ● 实时字幕      [AI]     系统声音 → SenseVoice → 透明字幕   启动
  ● Ollama        [AI]     本地大模型服务 · ollama×1 端口11434  停止
  ...
```

## 跑起来

```bash
dotnet run                    # 开发运行
dotnet build -c Release       # 出 Release
```

需要 .NET 8 SDK（`winget install Microsoft.DotNet.SDK.8`）。

## 功能清单都在 `features.json`

程序**不含硬编码逻辑**，所有可启停项都从这个文件读，改完点界面上的「配置文件」按钮
或重启程序即可生效（不用重新编译）。

```jsonc
{
  "id": "live-subtitle",              // 唯一 id，PID 记录用它命名
  "name": "实时字幕",                  // 卡片标题
  "description": "系统声音 → SenseVoice 本地识别 → 透明悬浮字幕条",
  "category": "AI",                   // 卡片右上角标签
  "autoStart": false,                 // true = 程序启动时自动拉起

  "start": {
    "file": "D:\\path\\pythonw.exe",  // exe / bat / cmd，PATH 里的名字也行
    "args": "live_subtitle.py --engine sensevoice",
    "workingDirectory": "D:\\path",
    "useShellExecute": false,          // bat / UWP 需要 true
    "runAsAdmin": false                // true 会弹 UAC
  },

  "stop": {
    "killByPid": true,                 // 优先结束自己启动的那个 PID（默认 true）
    "killProcessNames": ["LunaTranslator"],  // 兜底：按进程名结束
    "command": "docker.exe",           // 或者先跑一条停止命令
    "commandArgs": "stop rtd-oracle"
  },

  "detect": {
    "processNames": ["ollama"],        // 进程名（不带 .exe）出现 = 运行中
    "port": 11434,                     // 本地端口在监听 = 运行中
    "matchAll": false                  // true = 全部满足才算运行中
  }
}
```

### 判定规则
- `detect` 里给的条件**任一满足**即视为运行中（除非 `matchAll: true`）。
- `stop.killByPid` 为真时还会用启动时记下的 PID 判断，最精确。
- PID 记在 `%APPDATA%\WindowsTool\pids\<id>.pid`。

### 几个来自实战的坑
- **bat 拉起来的 python 子进程杀不掉**：`killByPid` 记的是 cmd.exe，杀它不影响子进程。
  用 `scripts/stop-by-cmdline.ps1 -Pattern xxx` 按命令行匹配结束（ComfyUI 就是这么配的）。
- **进程名要能唯一定位**：`pythonw.exe` 这种不能作为 `detect.processNames`，
  会让别的脚本也误判成"运行中"。这类只用 PID 判定（`processNames: []`）。
- **端口判定最省心**：服务类（Ollama 11434 / ComfyUI 8188 / ai-gateway 8081）优先用端口。

## 代码结构

```
MainWindow.xaml(.cs)        界面：标题栏 / 概览 / 卡片墙 / 状态栏
Themes/Theme.xaml           配色与控件样式（取色自 linusonn.com）
Controls/StarfieldBackground.cs   星野粒子背景（漂移 + 呼吸 + 鼠标视差 + 星云）
Features/IFeature.cs        功能抽象（状态机 + 启停 + 刷新）
Features/FeatureConfig.cs   features.json 的模型
Features/ProcessFeature.cs  通用实现：进程 / 端口 / PID 三种探测 + 启停
Features/FeatureManager.cs  加载配置、批量刷新、全部启停
scripts/stop-by-cmdline.ps1 按命令行结束进程（bat 子进程场景）
```

加一个新功能**不需要写 C#** —— 往 `features.json` 里加一段就行。

## 当前已接入

| 功能 | 探测方式 | 说明 |
|---|---|---|
| 实时字幕 | PID | 本地 SenseVoice，透明悬浮字幕条 |
| 系统实时字幕 | 进程 | Windows 11 自带 LiveCaptions |
| Ollama | 进程 + 11434 | qwen3.5-9b / gemma4-e4b |
| AI 女友 | 8081 | 复用「启动AI女友.bat」/「停止AI女友.bat」 |
| ComfyUI | 8188 | 复用「启动ComfyUI.bat」 |
| LunaTranslator | 进程 | 屏幕 OCR / 游戏文本翻译 |
| Docker Desktop | 进程 | 容器运行时 |
| RTD 数据库 | 1521 | rtd-oracle + rtd-redis |
