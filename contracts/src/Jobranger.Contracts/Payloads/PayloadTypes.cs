using System.Text.Json.Serialization.Metadata;
using Jobranger.Contracts.Ads;
using Jobranger.Contracts.Applications;
using Jobranger.Contracts.Learning;
using Jobranger.Contracts.Profiles;
using Jobranger.Contracts.Replies;
using Jobranger.Contracts.Runs;
using Jobranger.Contracts.Settings;

namespace Jobranger.Contracts.Payloads;

internal static class PayloadTypes {
    private static readonly ContractsJsonContext context = ContractsJsonContext.Default;

    public static IReadOnlyDictionary<string, JsonTypeInfo> ByTag { get; } = new Dictionary<string, JsonTypeInfo>(StringComparer.Ordinal) {
        [Status.Tag] = context.Status,
        [AdsSnapshot.Tag] = context.AdsSnapshot,
        [ApplicationsSnapshot.Tag] = context.ApplicationsSnapshot,
        [ConfigSnapshot.Tag] = context.ConfigSnapshot,
        [ProfileSnapshot.Tag] = context.ProfileSnapshot,
        [UsageSnapshot.Tag] = context.UsageSnapshot,
        [ModelsSnapshot.Tag] = context.ModelsSnapshot,
        [GuidanceSnapshot.Tag] = context.GuidanceSnapshot,
        [ContextSnapshot.Tag] = context.ContextSnapshot,
        [LearningSnapshot.Tag] = context.LearningSnapshot,
        [RunProgress.Tag] = context.RunProgress,
        [RunFinished.Tag] = context.RunFinished,
        [RunCancelled.Tag] = context.RunCancelled,
        [Saved.Tag] = context.Saved,
        [ErrorMessage.Tag] = context.ErrorMessage,
        [Unpaired.Tag] = context.Unpaired,
        [RunStart.Tag] = context.RunStart,
        [RunCancel.Tag] = context.RunCancel,
        [AdMark.Tag] = context.AdMark,
        [AdUnmark.Tag] = context.AdUnmark,
        [AdShowAnyway.Tag] = context.AdShowAnyway,
        [ApplicationCreate.Tag] = context.ApplicationCreate,
        [ApplicationUpdate.Tag] = context.ApplicationUpdate,
        [ApplicationRemove.Tag] = context.ApplicationRemove,
        [ConfigUpdate.Tag] = context.ConfigUpdate,
        [ModelSelect.Tag] = context.ModelSelect,
        [KeyReplace.Tag] = context.KeyReplace,
        [CvUpload.Tag] = context.CvUpload,
        [ProfileUpdate.Tag] = context.ProfileUpdate,
        [GuidanceUpdate.Tag] = context.GuidanceUpdate,
        [GuidanceReset.Tag] = context.GuidanceReset,
        [ContextUpdate.Tag] = context.ContextUpdate,
        [ContextUpload.Tag] = context.ContextUpload,
        [ContextRemove.Tag] = context.ContextRemove,
        [LearningForget.Tag] = context.LearningForget,
        [LearningForgetAll.Tag] = context.LearningForgetAll,
        [Unpair.Tag] = context.Unpair,
        [Chunk.Tag] = context.Chunk,
    };
}
