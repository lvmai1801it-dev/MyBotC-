using System.Reflection;
using FluentAssertions;
using MyBot.Core.Abstractions;
using MyBot.Core.Enums;
using OpenCvSharp;

namespace MyBot.Core.Tests;

public class AbstractionsTests
{
    // Test Case T0.2.1: Tất cả interface phải có ≥ 2 method và không có setter
    [Fact]
    public void AllInterfaces_ShouldFollowContract()
    {
        // Lấy tất cả các interface trong assembly MyBot.Core
        var interfaces = typeof(ITimeProvider).Assembly.GetTypes()
            .Where(t => t.IsInterface && t.Namespace != null && t.Namespace.StartsWith("MyBot.Core"))
            .ToList();

        interfaces.Should().NotBeEmpty();

        foreach (var type in interfaces)
        {
            // 1. Phải có ít nhất 2 method/property (bao gồm cả kế thừa từ IDisposable)
            var methods = type.GetMethods().Concat(type.GetProperties().Select(p => p.GetMethod!));
            methods.Count().Should().BeGreaterThanOrEqualTo(2, $"Interface {type.Name} không đủ method");
            
            // 2. Không được có property setter (chỉ đọc - readonly)
            foreach (var prop in type.GetProperties())
            {
                prop.CanWrite.Should().BeFalse($"Interface {type.Name} không được có setter cho property {prop.Name}");
            }
        }
    }

    // Test Case T0.2.2: IStateClassifier phải nhận vào Mat và trả về Enum GameState
    [Fact]
    public void StateClassifier_ShouldTakeMatAndReturnEnum()
    {
        var method = typeof(IStateClassifier).GetMethod("Classify");
        
        method.Should().NotBeNull();
        
        // Tham số đầu vào phải là OpenCvSharp.Mat
        method!.GetParameters()[0].ParameterType.Should().Be(typeof(Mat));
        
        // Kiểu trả về phải là Enum GameState (không phải string)
        method.ReturnType.Should().Be(typeof(GameState));
        method.ReturnType.IsEnum.Should().BeTrue();
    }
}
