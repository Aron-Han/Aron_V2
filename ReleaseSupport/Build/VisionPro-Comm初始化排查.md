# VisionPro Comm.dll 0x8007045A 排查与修复（2026-10-23）

## 确认的原因

本机安装的 VisionPro 为 9.6 SR2，目标程序集版本 71.2.0.0，程序为 x64。输出、GAC、ReferencedAssemblies 中的 Cognex.VisionPro.Comm.dll 内容一致，SHA-256 均为：

`212823223208287555CDC14ED2A779191A4C6279F270659DFE79873923B67C7D`

失败启动环境缺少 `C:\Program Files\Cognex\VisionPro\bin` 的进程 PATH 项。GAC 能找到托管程序集，但 Cognex 混合模式初始化仍依赖原生运行环境目录。没有替换 DLL 或升级版本，只在隔离测试进程添加该目录，同一 Assembly.Load 从约 22 秒失败变成约 1.5 秒成功。先加载 Core 并不能绕开这个问题：缺少目录时 Core 同样失败。这里确认的是目录缺失导致初始化失败，并未确定到某个单独的内部动态依赖文件。

GoSDK 在失败前没有加载；不是其 6.5.2.5 与 VisionPro 的版本混用。原已运行的主程序也从同一 GAC 位置加载了这些 Cognex DLL。

## 修复范围

`VisionProRuntimeAudit` 在第一次混合模式 Assembly.Load 之前准备原生运行环境：

1. 从 VPRO_ROOT、x64 安装注册信息和标准 Program Files 位置寻找安装目录。
2. 检查安装目录 ReferencedAssemblies/Core 的精确版本、签名，以及 cogxstd.dll 原生版本的主/次/构建号。原生补丁修订号允许与托管程序集不同。错误版本不加入搜索路径。
3. 将匹配的 bin 放到当前进程 PATH 首位，保留其它 SDK 的原路径；不会累加重复项。
   后续编辑器检查另发现 VPRO_ROOT 缺失，现同时将进程级 VPRO_ROOT 指向相同、已校验版本的安装根目录，供工具树图标/资源使用，不改系统/用户变量。
4. 日志记录 `NATIVE_RUNTIME` 目录及版本；找不到匹配安装时明确报错，不静默加载其它版本。

不修改系统或用户 PATH、注册表、Cognex 安装目录、绑定重定向，不调用 SetDllDirectory/SetDefaultDllDirectories；GoSDK 继续从应用 LmiSdk 使用绝对路径和原有 LoadLibraryEx 策略加载。未修改任何 PLC 通讯或相机采集逻辑。

## 验证

- 最小 Comm 只加载探针：缺少目录时约 22 秒失败，进程内添加目录后约 1.5 秒成功。
- 修复后整套 `--visionpro-check`：约 2.4 秒通过；只检查依赖，不启动主 UI/相机/PLC。
- 正确版本加载、缺少 manifest、混入 99.0.0.0 伪 DLL：分别通过或按预期拒绝。
- 路径准备测试：拒绝错误版本、不改机器/用户 PATH、重复调用不膨胀、保留其它 SDK 路径。
- GoSDK 的源目录和输出仍由原有 SHA-256 manifest 校验；测试同进程先 VisionPro 后 GoSDK、先 GoSDK 后 VisionPro，均不创建 GoSystem 或连接设备。

编译及依赖/模拟通讯验证不代替相机、授权、VPP 内容或现场运行验收。后续在 VS 中重新启动程序即可；已经运行的旧进程不会被本次修复强制关闭或修改其环境。
