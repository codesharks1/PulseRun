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

## 备份新的修改

先保存 Unity 场景及资源，再在项目目录运行：

```sh
git add .
git commit -m "Describe the changes"
git push
```

本仓库未配置自动推送。本地保存不会立即更新 GitHub。

音乐等第三方素材的权利属于原作者；私有备份不代表取得再分发或商业使用授权。
