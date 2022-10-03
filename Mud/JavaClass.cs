using System.Reflection;
using System;
using System.Linq;
using System.Reflection;

namespace Mud;

// Simple sample DispatchProxy-based logging proxy
public class JavaClass<T>  : DispatchProxy where T : class
{
    private T Target { get; set; } = null!;

    // DispatchProxy's parameterless ctor is called when a 
    // new proxy instance is Created
    public JavaClass() : base()
    {
    }

    // This convenience method creates an instance of DispatchProxyLoggingDecorator,
    // sets the target object, and calls DispatchProxy's Create method to retrieve the 
    // proxy implementation of the target interface (which looks like an instance of T 
    // but calls its Invoke method whenever an API is used).
    public static T Decorate(T? target = null)
    {
        // DispatchProxy.Create creates proxy objects
        var proxy = (Create<T, JavaClass<T>>() as JavaClass<T>)!;

        // If the proxy wraps an underlying object, it must be supplied after creating
        // the proxy.
        proxy.Target = target ?? Activator.CreateInstance<T>();

        return (proxy as T)!;
    }

    // The invoke method is the heart of a DispatchProxy implementation. Here, we
    // define what should happen when a method on the proxied object is used. The
    // signature is a little simpler than RealProxy's since a MethodInfo and args
    // are passed in directly.
    protected override object Invoke(MethodInfo targetMethod, object[] args)
    {
        try
        {
            if (!targetMethod.IsSpecialName)
            {
                return targetMethod.Invoke(Target, args)!;    
            }
            
            
            // Perform the logging that this proxy is meant to provide
            Console.WriteLine("Calling method {0}.{1} with arguments {2}", targetMethod.DeclaringType.Name, targetMethod.Name, string.Join(", ", args.Select(o => o.ToString())));

            // For this proxy implementation, we still want to call the original API 
            // (after logging has happened), so use reflection to invoke the desired
            // API on our wrapped target object.
            var result = targetMethod.Invoke(Target, args);

            // A little more logging.
            Console.WriteLine("Method {0}.{1} returned {2}", targetMethod.DeclaringType.Name, targetMethod.Name, result);

            return result;
        }
        catch (TargetInvocationException exc)
        {
            // If the MethodInvoke.Invoke call fails, log a warning and then rethrow the exception
            Console.WriteLine("{0}\nMethod {1}.{2} threw exception: {3}", exc.InnerException, targetMethod.DeclaringType.Name, targetMethod.Name, exc.InnerException);
            throw exc.InnerException!;
        }
    }
}
