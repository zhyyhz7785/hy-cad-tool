步骤 1. 启 AutoCAD（不开 DWG 无所谓）

步骤 2. 命令行：NETLOAD → 选 ReCall\bin\Debug\ReCall.dll

步骤 3. 输 "C2"

​        期望看到：

​          C2 #1 完成 XXXms (复制X + 加载X + Terminate0 + InitializeXX + 表X)

​          命令表：66 条已配置 + 50 条占位符，解析失败 0 条

​          提示: 新命令只需改 commands.json 并用 N1~N50 占位符；改 CommandFacade.cs 才需关 CAD 重 NETLOAD ReCall。

步骤 4. 输 "hyRecallSelfCheck" → 粘输出给我，逐节对照诊断

步骤 5. 输 "hy" → 面板应弹出

步骤 6. 打开任一 DWG，输 "gj" → 应走到选实体 prompt（验证前置钩子 + 反射普通命令）

步骤 7. 输 "_HyExec" 前后触发一次面板按钮（验证 _HyExec 特殊分支）

步骤 8. 改 commands.json 把 "N1" 指向任一现成命令（如指向 DimensionAlignCommand） → C2 → 输 "N1" → 应执行

步骤 9.（可选）连续 C2 两次，看 "Terminate" 耗时不是 0（说明旧实例被正确清理）