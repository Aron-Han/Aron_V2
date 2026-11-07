# Database：表格与记录详情

按 B 方案调整界面，保留现有查询、时间条件、每页 50 条、翻页、CSV 导出、清空和双击查看输出项逻辑。

- 顶部：Start、End、Filter，右侧 Export。
- 左侧：原九列表格，保持列对象、绑定键和原始值；长表格可横向滚动。
- 右侧：选中行的 Id、Serial、Result、Job、Cam、PosN、Channel、CreatedAt、Payload。可拖动中间分隔条调节宽度。
- Payload 为只读、多行、自动换行，不拆分或改写原始内容，也不增加数据库查询。
- 底部：Clear All 位于左侧，与右侧 Previous、页码、Next 分开。
- Add Row 示例按钮已从界面移除；生产检测使用的 `InsertInspectionRecord` API 不变。

新增布局和详情代码位于 `Form/DataBase.Presentation.cs`。数据换页期间暂停详情读取，完成绑定后刷新，避免读到正在清空的旧选中行。

验证脚本 `Tests/DatabaseLayoutBTests.cs` 必须编译到独立新目录：数据库路径跟随测试 EXE 的 BaseDirectory，因此测试不会打开正式输出目录中的数据库。验证包含 55 条隔离样本、原分页和时间筛选、列宽保留、选中行完整值、CSV 特殊字符、空结果清空详情、中英文和不同窗口尺寸；不启动相机、算法或 PLC。
