using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TecnmInventario.Api.Common.Ai;

namespace TecnmInventario.Api.Features.Assistant.Shared;

/// <summary>
/// Respaldo: a veces Qwen escribe la llamada como texto (&lt;tool_call&gt;&lt;function=x&gt;&lt;parameter=k&gt;v...) y llama.cpp
/// no la reconoce. Se convierte en una llamada real para no mostrarle ese texto al usuario.
/// </summary>
public static partial class TextToolCalls
{
    public static AiChatReply Recover(AiChatReply reply)
    {
        if (reply.ToolCalls.Count > 0 || reply.Content is null || !reply.Content.Contains("<function=", StringComparison.Ordinal))
            return reply;

        var calls = new List<AiToolCall>();
        foreach (Match function in Function().Matches(reply.Content))
        {
            var args = new JsonObject();
            foreach (Match parameter in Parameter().Matches(function.Groups["body"].Value))
                args[parameter.Groups["name"].Value.Trim()] = parameter.Groups["value"].Value.Trim();
            calls.Add(new AiToolCall("txt" + calls.Count, function.Groups["name"].Value.Trim(), args.ToJsonString()));
        }
        if (calls.Count == 0) return reply;

        var message = new JsonObject
        {
            ["role"] = "assistant",
            ["tool_calls"] = new JsonArray(calls.Select(c => (JsonNode)new JsonObject
            {
                ["id"] = c.Id, ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = c.Name, ["arguments"] = c.Arguments },
            }).ToArray()),
        };
        return new AiChatReply(null, calls, message);
    }

    [GeneratedRegex(@"<function=(?<name>[^>]+)>(?<body>.*?)</function>", RegexOptions.Singleline)]
    private static partial Regex Function();

    [GeneratedRegex(@"<parameter=(?<name>[^>]+)>(?<value>.*?)</parameter>", RegexOptions.Singleline)]
    private static partial Regex Parameter();
}
