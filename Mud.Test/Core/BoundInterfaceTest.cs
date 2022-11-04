using Mud.Exceptions;
using Mud.Test.Core.Interfaces;
using Xunit;
using Xunit.Abstractions;

namespace Mud.Test.Core;

[Collection("Serial")]
public class BoundInterfaceTest : BaseTest
{
    private readonly ITestOutputHelper _testOutputHelper;

    public BoundInterfaceTest(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void GenericReturnTypeTest()
    {
        var strBldr = ClassInfo<IStringBuilder>.Instance("Foo").Append('+');
            
        var genericResp = strBldr.AppendWithGenericResp("Bar");
        // ReSharper disable once SuspiciousTypeConversion.Global
        Assert.True(genericResp is IStringBuilder);
        strBldr.Append('=').AppendWithGenericResp("FooBar");
        Assert.Equal("Foo+Bar=FooBar", strBldr.ToString());
    }
    
    [Fact]
    public void StaticGenericReturnTypeTest()
    {
        var strBldr = ClassInfo<IStringBuilder>.Instance("Foo").Append('+');
            
        var genericResp = strBldr.AppendWithGenericResp("Bar");
        // ReSharper disable once SuspiciousTypeConversion.Global
        Assert.True(genericResp is IStringBuilder);
        strBldr.Append('=').AppendWithGenericResp("FooBar");
        Assert.Equal("Foo+Bar=FooBar", strBldr.ToString());
    }
    
    [Fact]
    public void InvalidGenericParameterType()
    {
        var integerA = ClassInfo<IInteger>.Instance(10);
        var integerB = ClassInfo<IInteger>.Instance(8);
        Assert.Throws<MemberNotFoundException>(() => integerA.CompareToWithBadType(integerB));
    }
    
    [Fact]
    public void ValidGenericParameterType()
    {
        var integerA = ClassInfo<IInteger>.Instance(10);
        var integerB = ClassInfo<IInteger>.Instance(8);
        var resp = integerA.CompareTo(integerB);
        Assert.Equal(1, resp);
    }

    [Fact]
    public void StaticField()
    {
        var pi = ClassInfo<IMath>.Static.Pi;
        Assert.Equal(Math.PI, pi);
    }
    
    [Fact]
    public void StaticCall()
    {
        var cos = ClassInfo<IMath>.Static.Cos(35);
        Assert.Equal(-0.9036922050915067, cos);
        var sin = ClassInfo<IMath>.Static.sin(35);
        Assert.Equal(-0.428182669496151, sin);
    }
    
    [Fact]
    public void InstanceCallOnStaticException()
    {
        Assert.Throws<InstanceMemberOnStaticException>(() => ClassInfo<IMath>.Static.tan(35));
    }
}