# PulseRun

霓虹风格 Unity 音乐跑酷游戏。此私有仓库用于项目备份和代码阅读。

## 打开项目

1. 安装 Git LFS，运行 `git lfs install`。
2. 克隆仓库，然后在项目目录运行 `git lfs pull`。
3. 用 Unity Hub 添加克隆的项目文件夹。编辑器版本见 `ProjectSettings/ProjectVersion.txt`。
4. 打开 `Assets/PulseLook/NeonRunway.unity`。

`Assets`、`Packages` 和 `ProjectSettings` 已备份。Unity 缓存和本机设置不会上传，首次打开时 Unity 会重新导入资源。

## 主要代码

- `Assets/Scripts/Player/RunnerController.cs`：移动、跳跃、独立重力、跳跃预输入和起跳光圈。
- `Assets/Scripts/Player/RunnerRespawn.cs`：障碍碰撞、死亡碎片和重置。
- `Assets/Scripts/Music/RunMusicClock.cs`：音乐播放与时间轴。
- `Assets/Scripts/Music/RunChartRecorder.cs`：记录起跳与悬挂障碍时间。
- `Assets/Scripts/Music/RunChart.cs`：谱面数据。
- `Assets/Editor/RunChartWindow.cs`：谱面录制窗口与障碍生成。
- `Assets/Scripts/Music/RunChartMotion.cs`：按节拍密度规划加速段，以及变速后的路程计算。
- `Assets/Scripts/Music/RunnerRhythmMotion.cs`：根据歌曲时间应用跑速、重力与远景模糊。
- `Assets/Scripts/Player/RunnerBerserkVisuals.cs`：紫色狂暴模式、喷射、随机电弧生命周期和镜头表现。
- `Assets/Scripts/Player/RhythmNotePickup.cs`：悬挂障碍下的落地音符奖励、bling 声与重试恢复。
- `Assets/Scripts/Player/RunwayLightTrail.cs`：两侧短灯条根据实际跑过位置变色。
- `Assets/Scripts/Music/NeoCitySequence.cs`：23秒隧道转场、城市显隐和装饰车流。

## 当前进度与交接

本轮主要打磨前23秒：跳跃/保持落地两类谱面、X/C光门加速、落地音符反馈、对称变色灯带、紫电狂暴表现；约23秒进入隧道出口后的霓虹城市。加速由歌曲时间驱动，光门不是实际速度切换碰撞器。

明天继续时先看 `Docs/PulseRun-交接文档-2026-10-13.md`，再看 `Docs/代码阅读路线.md`。电弧等表现参数的说明在 `Docs/紫电狂暴表现.md`。

构建列表当前仍是 SampleScene；打包前请把 `Assets/PulseLook/NeonRunway.unity` 设为首个启用场景。未配置自动云同步，修改后仍需保存、提交和推送。

## 备份新的修改

先保存 Unity 场景及资源，再在项目目录运行：

```sh
git add .
git commit -m "Describe the changes"
git push
```

本仓库未配置自动推送。本地保存不会立即更新 GitHub。

音乐等第三方素材的权利属于原作者；私有备份不代表取得再分发或商业使用授权。
