// See https://aka.ms/new-console-template for more information


using Mud;
using Mud.Types;

// var jclass = new FileInfo(args[0]);
// Console.WriteLine("Hello, World!");

var jvm = new Jvm();
var sigParser = jvm.CallStatic<JavaObject>("sun.reflect.generics.parser.SignatureParser", "make");
sigParser.Call<();

