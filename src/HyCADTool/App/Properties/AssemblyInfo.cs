using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("HyCADTool")]
[assembly: AssemblyDescription("Refactored AutoCAD Plugin with Clean Architecture")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("HyCADTool Team")]
[assembly: AssemblyProduct("HyCADTool")]
[assembly: AssemblyCopyright("Copyright © 2025")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// 为单元测试项目暴露 internal 类型 / 方法（仅限测试，不暴露给生产 / ReCall 代码）
[assembly: InternalsVisibleTo("HyCADTool.Tests")]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("8a9f5c3e-2b4d-4f6e-9c8a-1d7e3f5b6c9a")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

