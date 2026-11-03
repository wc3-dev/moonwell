using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace Moonwell.Tests;

/// <summary>What Moonwell keeps from a map: anything a map can reach, it may come to depend on.</summary>
public class EncapsulationTests
{
    /// <summary>The methods only the code the build writes calls, and the classes that hold nothing else.</summary>
    private static readonly (Type Type, string[] Names)[] BuildOnly =
    {
        (typeof(ObjectTypes), new[] { "Unit", "Item", "Destructable", "Ability", "Id", "BuffDisplay" }),
        (typeof(Runtime), new[] { "Start" }),
        (typeof(DamageSystem), new[] { "Install" }),
        (typeof(Terrain), new[] { "Install" }),
        (typeof(AreaSystem), new[] { "Install" }),
        (typeof(InputSystem), new[] { "Install" }),
        (typeof(Physics), new[] { "Install" }),
    };

    private static bool Hidden(MemberInfo member)
    {
        return member.GetCustomAttribute<EditorBrowsableAttribute>()?.State == EditorBrowsableState.Never;
    }

    [Fact]
    public void NoHostingTypeIsPublic()
    {
        var open = Packages.All
            .SelectMany(assembly => assembly.GetExportedTypes())
            .Where(type => type.Namespace == "Moonwell.Hosting")
            .Select(type => type.FullName)
            .ToList();
        Assert.True(open.Count == 0, "public test seams: " + string.Join(", ", open));
    }

    [Fact]
    public void WhatOnlyTheBuildCallsIsHidden()
    {
        foreach (var (type, names) in BuildOnly)
        {
            foreach (var name in names)
            {
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(method => method.Name == name).ToList();
                Assert.NotEmpty(methods);
                Assert.All(methods, method => Assert.True(Hidden(method), type.Name + "." + name + " is not hidden"));
            }
        }

        Assert.All(new[] { typeof(DamageSystem), typeof(AreaSystem), typeof(InputSystem) }, type => Assert.True(Hidden(type), type.Name + " is not hidden"));
    }

    [Fact]
    public void WhatTheBuildNeverWroteIsNotPublic()
    {
        Assert.Equal(2, typeof(Runtime).GetMethods(BindingFlags.Public | BindingFlags.Static).Count(method => method.Name == "Start"));
        Assert.Single(typeof(InputSystem).GetMethods(BindingFlags.Public | BindingFlags.Static), method => method.Name == "Install");
    }

    [Fact]
    public void ObjectTypesOffersAMapOnlyItsIds()
    {
        var shown = typeof(ObjectTypes).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => !Hidden(method))
            .Select(method => method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(new[] { "FourCC", "IdOf" }, shown);
    }

    [Fact]
    public void BurstHitsIsReadOnly()
    {
        Assert.Equal(typeof(IReadOnlyList<Hit>), typeof(Burst).GetProperty(nameof(Burst.Hits))!.PropertyType);
    }

    [Theory]
    [InlineData(typeof(Game))]
    [InlineData(typeof(MoonwellException))]
    [InlineData(typeof(TerrainHit))]
    [InlineData(typeof(SpecialEffect))]
    public void OnlyMoonwellMakes(Type type)
    {
        Assert.Empty(type.GetConstructors());
    }

    [Theory]
    [InlineData(typeof(Scoped))]
    [InlineData(typeof(Scope))]
    [InlineData(typeof(Actor))]
    [InlineData(typeof(Widget))]
    [InlineData(typeof(MapComponent))]
    public void AMapCannotDeriveFromABaseClass(Type type)
    {
        var constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotEmpty(constructors);
        Assert.All(constructors, constructor => Assert.True(constructor.IsAssembly, type.Name + " has a constructor a map can reach"));
    }
}
