using Atc.Test;
using FluentAssertions;
using CurlGenerator.Core;

namespace CurlGenerator.Tests;

public class SerializerTests
{
    [Theory, AutoNSubstituteData]
    public void Can_Serialize_Settings(
        Settings settings)
    {
        Serializer
            .Serialize(settings)
            .Should()
            .NotBeNullOrWhiteSpace();
    }

    [Theory, AutoNSubstituteData]
    public void Can_Deserialize_Settings(
        Settings settings)
    {
        var json = Serializer.Serialize(settings);
        Serializer
            .Deserialize<Settings>(json)
            .Should()
            .BeEquivalentTo(settings);
    }

    [Theory, AutoNSubstituteData]
    public void Deserialize_Is_Case_Insensitive(
        Settings settings)
    {
        var json = Serializer.Serialize(settings);
        foreach (var property in typeof(Settings).GetProperties())
        {
            var jsonProperty = "\"" + property.Name + "\"";
            json = json.Replace(
                jsonProperty, 
                jsonProperty.ToUpperInvariant());
        }

        Serializer
            .Deserialize<Settings>(json)
            .Should()
            .BeEquivalentTo(settings);
    }
}