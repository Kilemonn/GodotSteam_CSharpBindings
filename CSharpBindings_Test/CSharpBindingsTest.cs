using System.Reflection;
using NUnit.Framework.Interfaces;

namespace CSharpBindings_Test
{
    public class CSharpBindingsTest
    {
        [Test]
        public void TestAllStaticMethods()
        {
            Assembly assembly = typeof(GodotSteam.Steam).Assembly;

            Type[] types = GetTypesInNamespace(assembly, "GodotSteam");
            Console.WriteLine("Found " + types.Length + " type(s).");
            BindingFlags bindingFlags = GetBindingStaticMethods();

            int count = 0;
            foreach (var type in types)
            {
                MethodInfo[] methodInfos = type.GetMethods(bindingFlags);
                count += methodInfos.Length;
                if (methodInfos.Length > 0)
                {
                    Console.WriteLine(type.Name + " with " + methodInfos.Length + " method(s).");
                    foreach (var methodInfo in methodInfos)
                    {
                        Console.WriteLine("Calling - " + type.Name + "." + methodInfo.Name);
                        object? result = InvokeMethod(null, methodInfo);
                        Assert.AreEqual(result.GetType(), methodInfo.ReturnType);
                    }
                }
            }
            Console.WriteLine("Total methods found: " + count);
        }

        private static object? InvokeMethod(object? obj, MethodInfo methodInfo)
        {
            ParameterInfo[] parameters = methodInfo.GetParameters();
            if (parameters == null)
            {
                parameters = Array.Empty<ParameterInfo>();
            }
            object?[]? defaultParams = GetParameterList(parameters);
            // methodInfo.Invoke(obj, Enumerable.Repeat<object>((object)GodotSteam.GameOverlayType.Achievements, 1).ToArray<object>());
            Assert.AreEqual(parameters.Length, defaultParams.Length);
            return methodInfo.Invoke(obj, defaultParams);
        }

        private static object?[]? GetParameterList(ParameterInfo[] parameters)
        {
            object?[]? methodParameters = Array.Empty<object>();
            foreach (var parameter in parameters)
            {
                object? constructed = null;

                if (parameter.ParameterType.IsEnum)
                {
                    constructed = parameter.ParameterType.GetEnumValues()
                        .OfType<Enum>()
                        .OrderBy(e => Guid.NewGuid()) // Do some random order by
                        .FirstOrDefault();
                }
                else
                {
                    constructed = parameter.GetType().GetConstructor(BindingFlags.Instance, Array.Empty<Type>()).Invoke(null);
                }
                if (constructed != null)
                {
                    methodParameters = methodParameters.Concat(new object[] { constructed }).ToArray<object>();
                }
            }
            return methodParameters;
        }

        private static BindingFlags GetBindingStaticMethods()
        {
            return BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic;
        }

        private static BindingFlags GetBindingInstanceMethods()
        {
            return BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        }

        private static Type[] GetTypesInNamespace(Assembly assembly, string nameSpace)
        {
            return assembly.GetTypes()
                .Where(t => String.Equals(t.Namespace, nameSpace, StringComparison.Ordinal)).ToArray();
        }
    }
}