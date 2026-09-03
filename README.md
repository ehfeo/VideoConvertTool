# VideoConvertTool（AVS 视频转码工具）

基于 **ffmpeg** 的 Windows 拖放式批量转码工具（.NET 10 WinForms），支持 **GPU 硬件加速编码**。

> 52pojie 出品

## 功能特性

- **拖放即用**：把视频/音频文件（或整个文件夹）拖进窗口即可批量转码
- **GPU 加速**：自动探测可用的硬件编码器（NVIDIA NVENC / Intel QSV / AMD AMF），按 GPU 品牌自动选择最优编码器；探测失败自动回退软件编码
- **多编码器支持**：H.264 / H.265 / AV1 / MPEG-4 / MPEG-2 / 仅封装(流复制)
- **两种码控模式**：
  - CRF 恒定质量（20–50，值越小越清晰，默认 40；NVENC 使用 `-rc vbr -cq N -b:v 0` 等效实现）
  - 指定码率（支持"源码率"跟随输入文件）
- **音频处理**：复制 / AAC / AC-3 / MP3 / FLAC，可选码率
- **滤镜**：yadif 去隔行、分辨率缩放（1080p/720p/540p/360p）
- **输出格式**：MKV / MP4 / TS / MOV / AVI / WEBM / MPEG
- **实时进度**：进度条 + 已处理时长/总时长 + 转码倍速（speed）
- **配置记忆**：输出目录、ffmpeg 路径、编码参数自动保存到 ini

## 使用方法

1. 用 VS 或 `dotnet build` 编译（需要 .NET 10 SDK）
2. 把 `ffmpeg.exe`、`ffprobe.exe` 放到程序同目录（首次运行也可手动选择路径）
3. 拖入文件 → 选参数 → 开始转码

> 注意：ffmpeg 8.x 的 NVENC 需要 NVIDIA 驱动 ≥ 570（NVENC API 13.0）。
> 若日志提示"未探测到可用的 GPU 硬件编码器"，请先升级显卡驱动。

## GPU 加速说明

程序启动时后台探测硬件编码器（枚举 + 实际试转验证），探测结果输出到日志：

```
GPU 硬件编码器可用: h264_nvenc, hevc_nvenc
```

- 勾选「GPU加速」后按 H.264/H.265 自动选择 `*_nvenc → *_qsv → *_amf` 顺序中第一个可用的编码器
- **码率模式下只传 `-b:v`，不叠加 `-cq`**（NVENC 下 `-cq` 优先会导致码率失控）

## 项目结构

```
MainForm.cs             主界面 + 转码逻辑（命令行构建、进度解析、GPU 探测）
Program.cs              入口
VideoConvertTool.csproj 项目文件
app.manifest            DPI 感知清单
app.ico                 程序图标
```

## 依赖

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [ffmpeg](https://ffmpeg.org/download.html)（含 ffprobe，放在程序同目录）
