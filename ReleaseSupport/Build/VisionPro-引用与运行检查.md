# VisionPro 引用与运行检查

普通 Visual Studio 构建默认使用 VisionPro 9.6 SR2 x64，程序集版本 `71.2.0.0`。
项目优先使用 GAC 中明确版本的 DLL；没有对应文件才使用安装目录。实际解析后的版本、签名不符会使构建失败，不会用其他版本替代。
一键分版本构建仍以选择的 SDK 为目标，不会被固定成 9.6 SR2。

每次构建生成 `Visual Inspection.exe.visionpro-references.txt`，部署时必须与 EXE、EXE.config 一起携带。
构建检查输出目录中的 VisionPro DLL，发现遗留的其他版本会报错；不会自动删除用户文件、配置或 VPP。
不要添加跨版本的 Cognex bindingRedirect，也不要把其他版本 DLL 手工拷入输出目录。

程序启动前校验输出目录、清单和实际加载的托管 VisionPro 依赖链，记录程序集全名及实际路径；失败时不进入主程序、不连接设备。
日志位于程序目录 `Logs/VisionPro/`，无写权限时使用 `%LOCALAPPDATA%/Aron/Logs/VisionPro/`。
运行期间继续记录后来加载的 VisionPro 程序集及不匹配项；不会在 CLR 加载回调中强退正在工作的程序或更改通讯行为。
这不是 CLR 沙箱：后续 VPP/插件主动加载其他版本时，只记录异常，不承诺可以从进程里卸载已经加载的 DLL。

单独检查（不打开主窗体、不创建 Cognex COM 对象、不加载 VPP、不访问相机/PLC）：

```powershell
$p = Start-Process -FilePath '.\Visual Inspection.exe' -ArgumentList '--visionpro-check' -WindowStyle Hidden -Wait -PassThru
$p.ExitCode # 0=托管依赖检查通过；2=失败，查看日志
```

检查通过不等于 COM 注册、原生驱动、许可证或某个 VPP 文件兼容。VPP 仍应使用相同 VisionPro 版本开发/转换并独立验证。
