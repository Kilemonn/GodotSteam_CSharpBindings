using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using GodotSteam;
using Moq;

namespace CSharpBindings_Test
{
    public class CSharpBindingsTest
    {

        [Test]
        public void TestCompareMethodsInDLL()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Assert.NotNull(assembly);

            foreach(var file in assembly.GetFiles())
            {
                Console.WriteLine("File name: " + file.Name);
            }
        }

        [Test]
        public void TestAllStaticMethods()
        {
            // Check methods available in DLL
            // Compare with count of methods here (or method name check?)
            // Call all available methods from C# to check there are no runtime errors

            // Mock<GodotObject> steam = new(typeof(Steam));
            // Steam.Instance = steam.Object;
            
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
                        // Attribute? compilerGenerated = methodInfo.GetCustomAttribute(typeof(CompilerGeneratedAttribute));
                        // Attribute? extensionAttribute = methodInfo.GetCustomAttribute(typeof(ExtensionAttribute));
                        if (methodInfo.IsAbstract  || methodInfo.IsSpecialName /*|| compilerGenerated != null || extensionAttribute != null*/)
                        {
                            Console.WriteLine("Skipping abstract/compiler generated/extension method - " + type.Name + "." + methodInfo.Name);
                        }
                        else
                        {
                            Console.WriteLine("Calling - " + type.Name + "." + methodInfo.Name);
                            object? result = InvokeMethod(null, methodInfo);
                            Assert.AreEqual(result.GetType(), methodInfo.ReturnType);
                        }
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
                else if (parameter.ParameterType.IsValueType || parameter.ParameterType.IsPrimitive)
                {
                    constructed = Activator.CreateInstance(parameter.ParameterType);
                }
                else if (parameter.ParameterType == typeof(string)) // Special case for string since its a reference type
                {
                    constructed = "";
                }
                else
                {
                    Type type = parameter.GetType();
                    ConstructorInfo? constructorInfo = type.GetConstructor(BindingFlags.Instance, Array.Empty<Type>());
                    constructed = constructorInfo?.Invoke(null);
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
            return BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
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