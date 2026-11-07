# VisionPro 一键编译

在资源管理器中双击仓库根目录的 **Build-VisionPro.cmd**，按编号选择本机 VisionPro。无需打开 Visual Studio。

## 首次准备

- Windows，已安装待支持版本的 VisionPro SDK／运行库（x64）。只有运行时而没有所需参考程序集时，不能保证能够构建。
- Visual Studio 或 Visual Studio Build Tools，包含“.NET 桌面生成工具”和 .NET Framework 4.7.2 Targeting Pack。
- 本工程依赖的 `packages`、HslCommunication 以及现有第三方引用必须可用。脚本不会联网安装 SDK、自动升级依赖或修改系统 COM 注册。
- 工程仍有少量机器专属 HintPath（例如 SunnyUI 扩展）；换开发机出现缺失引用，需要先配置这些第三方依赖。脚本会保留编译错误，不会生成伪成功包。

## 操作步骤

1. 双击 `Build-VisionPro.cmd`。
2. 输入安装列表中的编号；`M` 可以手动输入 SDK 根目录或其 `ReferencedAssemblies` 目录；`Q` 取消。
3. 选择是否复制当前 `Aron_V2/bin/x64/Debug/Project` 的 `Config` 和 `VPP`。
   - 默认不复制。未复制时，产物只是程序包，直接启动并不等于拥有原项目的配置、相机或算法。
   - 复制时是文件快照，不打开、不转换、不覆盖原 VPP；根目录的 `users.xml`、数据库、统计和日志不复制。
   - `Config` 中仍会包含现场 IP、目录、通讯设置等，请在交付前检查；不要把生产数据包公开上传。
4. 编译结果和日志路径会显示在窗口中。按任意键关闭窗口。

默认生成 Release/x64。一个版本 API 不适用于当前源码时，会明确编译失败，需要适配源码后重新构建，不会靠修改版本号冒充兼容。

## 输出目录

```text
artifacts/VisionPro/<安装标签>-asm<Core程序集版本>/<Release或Debug>/<时间戳-唯一ID>/
  app/                         程序及托管依赖，可选的 Project/Config、Project/VPP
  runtime.json                 只有构建及引用校验全部成功后才写入
  build.log                    MSBuild 编译日志
  build-failed.txt              失败时的原因（成功构建没有此文件）
  work/                        隔离的 obj、所选引用清单、BuildInfo
```

每次建立新目录，不覆盖历史包，也不清空现有 `bin`、`obj`、配置、VPP。普通 VS 构建不受影响；一键构建不会改写 `.csproj`、`App.config` 或源码中的 `BuildInfo.cs`。

`runtime.json` 为后续启动器预留，记录入口、x64/net472、SDK 路径／身份、每项实际编译引用及 SHA256、项目数据是否包含、构建时间等。
`status=compiled` 仅表示编译成功，`runtimeValidation=not-performed` 表示尚未验证相机、授权、CC24、VPP 和算法结果。未来启动器不能把这份清单当成运行兼容认证，也不能仅靠扫描清单切换系统的原生运行库。

## 引用选择与校验

- 扫描环境变量、Cognex 常见安装目录和卸载注册表，只读取 DLL 元数据，不加载 VPP／Cognex 控件，不连接硬件。
- 使用选中 SDK 的 Core 程序集版本作为目标运行库身份，不把产品显示版本（例如 9.6 SR2）和程序集版本（例如 71.2.0.0）混为一谈。
- 如果参考目录里的某个直接引用与目标 Core 版本不同，仅允许显式使用系统 GAC 中**同名、同公钥、与目标版本完全一致**的程序集；日志和清单会列出来源，不查找“较新可替代”版本。
- 当前方案面向本工程的传统 .NET Framework VisionPro API。不能用它保证所有产品系列／所有不同程序集版本组合均适用；缺失同版本依赖、API 改动或 SDK 身份不一致时应停止，另做适配。
- 编译后复核 MSBuild 实际解析的每个 Cognex 直接引用路径，和所选清单不一致就拒绝生成 `runtime.json`。
- 临时文件独立、单进程构建、MSBuild node reuse 关闭；构建脚本不留后台视觉测试进程。

## 命令行

以下命令从仓库根目录执行，参数路径可以包含空格或中文。

```powershell
# 只列出检测结果（JSON），不编译
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build\Build-VisionPro.ps1 -List

# 无人值守编译，不复制项目数据
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build\Build-VisionPro.ps1 -VisionProPath "C:\Program Files\Cognex\VisionPro"

# 指定 Debug，并复制一个经过选择的项目快照
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build\Build-VisionPro.ps1 -VisionProPath "C:\Program Files\Cognex\VisionPro" -Configuration Debug -ProjectDataPath "D:\MyProject\Project"
```

可选 `-OutputRoot "D:\Build Output"` 指定产物根目录；`-MSBuildPath "完整的MSBuild.exe路径"` 指定构建工具。退出码 0 表示成功／人工取消，1 表示失败。构建失败不要使用未完成的 `app` 目录。

本机若只安装了一套 SDK，列表只显示这一套，不会伪造其他版本或下载 DLL。新增版本后，重新运行入口检测并进行编译、离线 VPP 加载、相机和通讯回归测试。
