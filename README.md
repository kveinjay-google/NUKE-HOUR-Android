# NUKE HOUR for Android

基于 [OpenRA](https://github.com/OpenRA/OpenRA) 和 [OpenRA/ra2](https://github.com/OpenRA/ra2) 的 Android ARM64 即时战略游戏引擎客户端。

An Android ARM64 RTS engine client based on OpenRA and the Red Alert 2 mod. This repository includes the current engine source, Android host, native pixel conversion code, build scripts, tests, and the reviewed UI resources required to build the app.

## 当前版本

- Android 0.0.31 / Build 31，目标 Android 7.0+、ARM64、OpenGL ES 3。
- 包含触控操作、经典/高清界面以及盟军、苏军、尤里阵营的 UI 素材。
- 包含最近的中立科技建筑、战斗要塞和 Android 内存、纹理回收、温控帧率优化。
- 支持八档 AI 难度、自定义预设、拓展科技建筑、导入进度与预计时间、导入后自动加载、每周官网更新检测，以及局域网浏览器导入（复制完整网址，无需手动配对码）。
- 这是整理后的独立源码快照；`engine/` 包含完整的当前引擎修改，不需要另外下载或覆盖引擎。

**仓库不包含原版《红色警戒 2》或《尤里的复仇》的游戏资源档案（MIX/BAG 等）。** 完整游戏需要用户从自己合法拥有的副本导入资源。项目原创背景视频和经过清单审核的界面素材已包含，不会因为选择不同 UI 风格而缺少素材。

## 构建 Android APK

当前验证平台为 macOS；脚本也提供 Linux NDK 主机路径支持，Linux 完整 APK 构建尚未验证。

依赖：

- .NET **8 SDK** 和 Android workload（`dotnet workload install android`）。
- JDK 17、Python 3.10+、CMake、curl、tar。
- Android SDK platform 34、Build Tools 35.0.0、NDK 28.2.13676358。

在 Android SDK 已安装且许可证已接受的环境中，设置路径并构建：

```sh
export ANDROID_HOME=/path/to/android-sdk
export JAVA_HOME=/path/to/jdk-17
export ANDROID_NDK_HOME="$ANDROID_HOME/ndk/28.2.13676358"

sh packaging/android/build-public-clean.sh
```

首次构建会从官方来源下载并编译 SDL 2.30.10、FreeType 2.13.3；之后编译本项目的原生像素处理代码和 .NET Android 客户端。编译最多使用 4 个并行作业，单个构建阶段设置外部超时。

输出：

```text
artifacts/android-public-clean/build/Release/net8.0-android/android-arm64/com.openra.android.personal-Signed.apk
```

构建包含资源哈希清单审计、各阵营/界面风格资源检查、签名校验和 16 KB ZIP 对齐校验。Release 默认不允许调试。项目没有提供私人签名密钥；默认 SDK 开发签名仅适用于本地安装，发行者应自行配置自己的发布签名。

不要运行旧版开发仓库的 `fetch-engine.sh`：本仓库已直接保留当前引擎源码。也不要向仓库添加导入的游戏文件、签名密钥或本机配置。

## 测试

测试项目仍使用 `net6.0` 目标，可直接使用本机 .NET 8 运行，不需要安装 .NET 6：

```sh
export DOTNET_ROLL_FORWARD=Major
export DOTNET_PROCESSOR_COUNT=4

dotnet test engine/OpenRA.Test/OpenRA.Test.csproj -c Release -m:4 \
  --filter 'AndroidThermalFramePolicyTest|ReloadableSheetTest|DirectSheetPixelsTest|ChromePngDecoderTest|AndroidVideoPixelsTest'

python3 -m unittest discover -s packaging/tests -p test_android_resource_release_gate.py
```

完整引擎测试源码也保留在仓库中；部分历史 iOS/私有测试夹具合同不适用于这个 Android 发行目录。公开目录的实际验证范围见 [构建验证记录](docs/BUILD_VERIFICATION.md)。

已在小米手机验证局域网大文件导入及自动进入主菜单；复制链接版本已安装，但微信转发操作尚未实机验收。目前没有针对性能改动完成安卓真机长时间验收。单元测试和 APK 构建通过，不代表已经测得帧率提升或完成所有机型的后台恢复、温控和稳定性测试。

## 目录

- `android/`：Android 生命周期、原生桥接、平台后端、构建脚本。
- `engine/`：基于 OpenRA `release-20250330` 的完整修改版引擎源码。
- `OpenRA.Mods.RA2/`：游戏逻辑扩展。
- `mods/`：可公开的规则、布局、本地化、字体和 UI 资源。
- `packaging/`：版本管理、资源白名单、APK 审计与测试。
- `third_party/`：保留许可头的共享源文件；无需另外的 iOS 仓库。

## 许可与来源

源码遵循 [GNU GPL v3 或更高版本](LICENSE)，保留各文件原有版权和单独许可声明。SDL/SDL2#、FreeType、字体和其他资源的许可与来源见 [第三方说明](docs/THIRD_PARTY_NOTICES.md) 和 [资源清单](packaging/public-content-manifest.json)。

NUKE HOUR 是独立开源项目，与 Electronic Arts 没有隶属、认可或赞助关系。本仓库不授予任何原版游戏数据的使用许可。清单中 `user-reference-restored` 类别的 UI 图片来自用户参考图的项目处理，保留这一来源标记；代码的 GPL 许可不替代素材各自的权利和授权。详见 [游戏资源说明](docs/legal/THIRD_PARTY_CONTENT.md)。

This project is not affiliated with, endorsed by, or sponsored by Electronic Arts. Original game data must be supplied by the user from a legally owned copy.
