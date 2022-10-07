// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using Mud;
using Mud.Types;
Console.WriteLine("Hello, World!");

// using var jvm = new Jvm("/Users/nicholas/Downloads/commons-math3-3.6.1/commons-math3-3.6.1.jar");
// using var jvm = new Jvm("-Djava.class.path=/Users/nicholas/Downloads/commons-math3-3.6.1/commons-math3-3.6.1.jar", "-Xlog:class+load=info:classloaded.txt");
using var jvm = new Jvm();

var integer = jvm.NewObj("java.lang.Integer", 10);
var intVal = integer.Call<int>("intValue");
// var intVal2 = jvm.CallStaticReturnType("java.lang.Integer", "valueOf", "java.lang.Integer",  10).Call<int>("intValue");
Console.WriteLine($"IntVal: [{intVal}]");
var integer2 = jvm.GetClassInfo("java.lang.Integer").CallStaticMethodNamedReturn("valueOf", "java.lang.Integer", 9);
var intVal2 = integer2.Call<int>("intValue");
Console.WriteLine($"IntVal2: [{intVal2}]");

// var strBldr = jvm.NewObj("java.lang.StringBuilder", "Foo");
// Console.WriteLine($"StrBldr: [{strBldr.Call<string>("toString")}]");

// using var jvm = new Jvm("-Xlog:class+load=info:classloaded.txt");

// jvm.AddClassPath("/Users/nicholas/Downloads/commons-math3-3.6.1/commons-math3-3.6.1.jar");

// var obj = jvm.CallStaticReturnType<JavaObject>(Java.Lang.ClassLoader, "");

// jvm.CallStatic("java.lang.System", "getProperty", "java.class.path");


// var descriptiveStats = jvm.NewObj("org.apache.commons.math3.stat.descriptive.DescriptiveStatistics");
// var values = new double[] { 65, 51, 16, 11, 6519, 191, 0, 98, 19854, 1, 32 };
// foreach (var value in values)
// {
//     descriptiveStats.Call("addValue", value);
// }
//
//
// var mean = descriptiveStats.Call<double>("getMean");
// var median = descriptiveStats.Call<double>("getPercentile", 50d);
// var standardDeviation = descriptiveStats.Call<double>("getStandardDeviation");
// Console.WriteLine(mean);
// Console.WriteLine(median);
// Console.WriteLine(standardDeviation);


// var db2File = jvm.NewObj<Db2File>();