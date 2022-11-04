# Mud
Mud is a library that provides the ability to easily interop with the Java Virtual Machine (JVM)

# NuGet
| Name | Package                                                                                            |
|------|----------------------------------------------------------------------------------------------------|
| Mud  | [![NuGet](https://img.shields.io/nuget/v/Mud.svg?label=NuGet)](https://www.nuget.org/packages/Mud) |


# Initializing JVM
To begin working with the JVM you must initialize it first by calling `Jvm.Initialize();`. You can also provide Java args to it by passing them into that method.<br>


```csharp
Jvm.Initialize("-Xms2G", "-Xmx8G", "-Djava.class.path=./libs/commons-math3-3.6.1.jar");
```

# Instantiate a Java Class

### Without an interface
Defining an interface is not required to create an instance of a Java class. For instance to create a `java.lang.StringBuilder` object you would first get the class info as below and then call the `Instance` method on the return `ClassInfo` object and pass in any required arguments, this returns an `IBoundObject` object that has a backing java object in the JVM.
```csharp
var strBldr = Jvm.GetClassInfo("java.lang.StringBuilder").Instance("Foo");
```
Then to call any methods on the returned bound object you would use any of the `Call` methods on the interface, where the first argument is always the name of the method on the Java class you want to call

```csharp
// This is using the call method that specifies the return type in the second argument
strBldr.Call("append", new CustomType("java.lang.StringBuilder"), '+')

// This is using the call method that specifies the return type as a type parameter
strBldr.Call<string>("toString")
```

To get/set and fields on the bound object you would use any of the `GetField` and the `SetField` methods on the interface, where the first argument is always the name of the field on the Java class you want to get/set.

 ```csharp
 // This is using the GetField method that specifies the return type as a type parameter
strBldr.GetField<int>("count")
```

---
### With an interface
While you can instantiate a Java class without an interface it can become very verbose when you start working with the object more than a couple times. Using and interface will reduce the amount of required typing as well as provide builtin type-checking when calling methods and getting/setting fields.

 Nothing is required for the interface to be automatically bound to the matching Java class if the .NETs type path (namespace+interface-name) matches the Java class-path and if the methods/fields names and types match <i>(Note: you are not required to define all methods/fields from the Java class, just the members you desire) </i>.
 
However, having everything match up is a rare occurence especially if you only want to use a small subsection of a Java library. So to be able to have an interface bound to a Java class with a custom .NET namespace or to have differing method/field names & types the library provides three helper attributes: 
1. `ClassPath` - Used to provide the correct Java class-path for the interface
2. `JavaName` - Used to provide the correct name that's in the Java class.
3. `JavaType` - Used to provide the Java class-path when using the generic `IBoundObject` interface as a return type or as parameters 

```csharp
// Providing a ClassPath attribute is not required if the .NET type path (namespace.typ-name) matches the Java class package
[ClassPath("java.lang.StringBuilder")]
public interface IStringBuilder
{
    // no attributes needed as the name, arguments and return type matches the java class
    public IStringBuilder append(string val);
    public IStringBuilder append(char[] val);
    public IStringBuilder append(bool val);
    
    // Since the method name in this interface does not match the java class method the JavaName attribute is used
    [JavaName("append")]
    public IStringBuilder AppendInt(int val);
    
    // Since the return type is the generic IBoundObject interface the JavaType attribute is required to specifiy the Java class path of the returned value
    [JavaType("java.lang.StringBuilder")]
    public IBoundObject append(int val);
    
    
    // For the below two methods we do not have a CharSequence interface nor a StringBuffer interface defined we need to use the JavaType attribute for the parameter since we will be passing in a generic IBoundObject for each of them
    [JavaName("append")]
    public IStringBuilder AppendCharSequence([JavaType("java.lang.CharSequence")] IBoundObject val);
    
    [JavaName("append")]
    public IStringBuilder AppendStringBuffer([JavaType("java.lang.StringBuffer")] IBoundObject val);
    
    public string toString();
    
    [JavaName("count")]
    public int Count { get; }
}
```

To instantiate your interface you would call the static `Instance` method on the `ClassInfo<T>` class with required arguments, this returns an instance of your interface that has a backing Java object in the JVM
```csharp
var strBldr = ClassInfo<IStringBuilder>.Instance("Foo");
```

You can then use the return interface object like any normal interface
```csharp
var strBldr = ClassInfo<IStringBuilder>.Instance("Foo");
// No need to provide the Java class's method name, nor the return type as that is done automatically
strBldr.Append('+');
strBldr.toString();

// No need to provide the Java class's field name, nor the type as that is also dont automatically
strBldr.Count;
```

 
# Static Fields/Methods

### Without an interface
Just as when instantiating an object it is not required to define an interface when working with static members. As before you must first get the `ClassInfo` via `Jvm.GetClassInfo`.

```csharp
var mathCls = Jvm.GetClassInfo("java.lang.Math");
var intCls = Jvm.GetClassInfo("java.lang.Integer");
```

Then to call any static method you would use any of the `CallMethod` methods, where the first argument is always the name of the static method on the Java class you want to call
```csharp
// This is using the call method that specifies the return type as a type parameter
mathCls.CallMethod<double>("cos", 35d);
// This is using the call method that specifies the return type in the second argument
intCls.CallMethod("valueOf", new CustomType("java.lang.Integer"),  10);
```
To get/set any static fields  you would use any of the GetField and the SetField methods, where the first argument is always the name of the static field on the Java class you want to get/set.

```csharp
// This is using the GetField method that specifies the return type as a type parameter
mathCls.GetField<double>("PI")
```

---
### With an interface
When working with any of the static members in a Java class via an interface you would mark them with the `JavaStatic` attribute, the same attributes as mentioned earlier work with these members as well. <i>(Note: you can provide the Java class member's name using the `JavaStatic` attribute if desired)</i>. 

```csharp
[ClassPath("java.lang.Math")]
public interface IMath
{
    [JavaStatic("PI")]
    public double Pi { get; }

    [JavaStatic]
    public double sin(double val);
    
    [JavaStatic("cos")]
    public double Cos(double val);
    
    [JavaStatic]
    public double tan(double val);
}
```

Then to use these static members you would access the static `Static` property on the `ClassInfo<T>`, this returns an static instance of your interface with backing calls to the static members in the Java class 

You can then use the return interface object like any interface object

```csharp
ClassInfo<IMath>.Static.Pi;
ClassInfo<IMath>.Static.Cos(35d);
```