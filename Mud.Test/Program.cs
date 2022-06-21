// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using Mud;
using Mud.Test;
using Mud.Types;

Console.WriteLine("Hello, World!");


using var jvm = new Jvm("/Users/nicholas/Documents/Dev/iSeries/Jt400Wrap/Jt400Wrap/dist/Jt400Wrap.jar");

var db2File = jvm.CallStaticCustomType<JavaObject>("Db2File", "Load", "Db2File", "TSTNJH", "B99FPQue");


// db2File.Call();
//myFile2.Open(AS400File.READ_ONLY, 1, AS400File.COMMIT_LOCK_LEVEL_NONE);

var timer = new Stopwatch();

db2File.Call("Open", 0, 1, 3);
timer.Start();
while (!db2File.GetProp<bool>("eof"))
{
    db2File.Call<string>("Read");
}
Console.WriteLine(timer.Elapsed.TotalMilliseconds);
db2File.Call("Close");

// var db2File = jvm.NewObj<Db2File>();