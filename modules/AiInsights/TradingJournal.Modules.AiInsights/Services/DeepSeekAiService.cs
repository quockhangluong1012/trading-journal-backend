using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Extensions;
using TradingJournal.Modules.AiInsights.Options;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Modules.AiInsights.Services;

internal sealed partial class DeepSeekAiService(
    IPromptService promptService,
    IAiTradeDataProvider tradeDataProvider,
    ITradeAiContextService tradeAiContextService,
    IEconomicImpactContextProvider economicImpactContextProvider,
    IRiskContextProvider riskContextProvider,
    ITradeProvider tradeProvider,
    IChecklistModelProvider checklistModelProvider,
    ISetupProvider setupProvider,
    HttpClient httpClient,
    IImageHelper imageHelper,
    IOptions<DeepSeekOptions> options) : IDeepSeekAiService
{
    private const int MaxChartAnalysisImages = 3;
    private const int MaxInlineImageBytes = 5 * 1024 * 1024;
    private const int MaxCoachReplyTokens = 2048;
    private static readonly TimeSpan CoachStreamTimeout = TimeSpan.FromMinutes(2);
    private static readonly Regex PromptCodeFencePattern = PromptCodeFencePatternRegex();
    private static readonly Regex PromptRolePrefixPattern = PromptRolePrefixPatternRegex();
    private static readonly HashSet<string> AllowedSetupNodeKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "start",
        "step",
        "decision",
        "end"
    };
    private static readonly string[] SupportedInlineImagePrefixes =
    [
        "data:image/png;base64,",
        "data:image/jpeg;base64,",
        "data:image/jpg;base64,",
        "data:image/webp;base64,"
    ];
}
