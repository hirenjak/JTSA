using JTSA.Utility;
using Newtonsoft.Json.Linq;
using Xunit;

namespace JTSA.Tests;

public class VtsProtocolTests
{
    [Fact]
    public void CreateEnvelope_FillsRequiredFields()
    {
        var envelope = VtsProtocol.CreateEnvelope("StatisticsRequest", new { hello = 1 }, "abc");

        Assert.Equal(VtsProtocol.ApiName, envelope.Value<string>("apiName"));
        Assert.Equal(VtsProtocol.ApiVersion, envelope.Value<string>("apiVersion"));
        Assert.Equal("abc", envelope.Value<string>("requestID"));
        Assert.Equal("StatisticsRequest", envelope.Value<string>("messageType"));
        Assert.Equal(1, envelope["data"]?.Value<int>("hello"));
    }

    [Fact]
    public void CreateEnvelope_RejectsEmptyMessageType()
    {
        Assert.Throws<ArgumentException>(() => VtsProtocol.CreateEnvelope(" "));
    }

    [Fact]
    public void CompleteRawJson_FillsMissingEnvelopeFields()
    {
        var envelope = VtsProtocol.CompleteRawJson("""{"messageType":"FaceFoundRequest"}""");

        Assert.Equal(VtsProtocol.ApiName, envelope.Value<string>("apiName"));
        Assert.Equal(VtsProtocol.ApiVersion, envelope.Value<string>("apiVersion"));
        Assert.False(string.IsNullOrWhiteSpace(envelope.Value<string>("requestID")));
        Assert.Equal("FaceFoundRequest", envelope.Value<string>("messageType"));
    }

    [Fact]
    public void CompleteRawJson_KeepsExistingRequestId()
    {
        var envelope = VtsProtocol.CompleteRawJson(
            """{"apiName":"VTubeStudioPublicAPI","apiVersion":"1.0","requestID":"keep-me","messageType":"APIStateRequest"}""");

        Assert.Equal("keep-me", envelope.Value<string>("requestID"));
    }

    [Fact]
    public void CompleteRawJson_RequiresMessageTypeAndValidJson()
    {
        Assert.Throws<ArgumentException>(() => VtsProtocol.CompleteRawJson("{"));
        Assert.Throws<ArgumentException>(() => VtsProtocol.CompleteRawJson("""{"apiName":"VTubeStudioPublicAPI"}"""));
        Assert.Throws<ArgumentException>(() => VtsProtocol.CompleteRawJson(""));
    }

    [Fact]
    public void IsAuthenticationSuccess_RequiresAuthenticatedTrue()
    {
        Assert.True(VtsProtocol.IsAuthenticationSuccess(JObject.Parse(
            """{"messageType":"AuthenticationResponse","data":{"authenticated":true}}""")));
        Assert.False(VtsProtocol.IsAuthenticationSuccess(JObject.Parse(
            """{"messageType":"AuthenticationResponse","data":{"authenticated":false}}""")));
        Assert.False(VtsProtocol.IsAuthenticationSuccess(JObject.Parse(
            """{"messageType":"APIStateResponse","data":{"authenticated":true}}""")));
    }

    [Theory]
    [InlineData("AuthenticationResponse", null, true, false, true)]
    [InlineData("AuthenticationResponse", null, false, false, false)]
    [InlineData("APIError", 50, true, null, true)]
    [InlineData("APIError", 51, true, null, true)]
    [InlineData("APIError", 1, true, null, false)]
    public void ShouldRequestNewToken_MatchesAuthFailures(
        string messageType, int? errorId, bool hadToken, bool? authenticated, bool expected)
    {
        var data = new JObject();
        if (authenticated is bool value)
            data["authenticated"] = value;
        if (errorId is int id)
            data["errorID"] = id;

        var response = new JObject
        {
            ["messageType"] = messageType,
            ["data"] = data
        };

        Assert.Equal(expected, VtsProtocol.ShouldRequestNewToken(response, hadToken));
    }

    [Fact]
    public void PluginIdentity_FitsVtsLengthLimits()
    {
        Assert.InRange(VtsProtocol.PluginName.Length, 3, 32);
        Assert.InRange(VtsProtocol.PluginDeveloper.Length, 3, 32);
    }
}
