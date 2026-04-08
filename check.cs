using System;
using System.Reflection;
using System.Linq;

namespace CheckMoq {
    class Program {
        static void Main() {
            var assembly = Assembly.LoadFrom(@"C:\Users\luong.quockhang_amar\.nuget\packages\mockqueryable.moq\10.0.5\lib\net8.0\MockQueryable.Moq.dll");
            var type = assembly.GetType("MockQueryable.Moq.MoqExtensions");
            foreach(var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public)) {
                if(method.Name == "BuildMockDbSet") {
                    var parameters = method.GetParameters();
                    Console.WriteLine(method.Name + " <" + string.Join(", ", method.GetGenericArguments().Select(g => g.Name)) + ">");
                    foreach(var p in parameters) {
                        Console.WriteLine("  " + p.ParameterType.FullName ?? p.ParameterType.Name);
                    }
                }
            }
        }
    }
}
