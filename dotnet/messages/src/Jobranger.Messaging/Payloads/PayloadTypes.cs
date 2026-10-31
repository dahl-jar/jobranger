using System.Text.Json.Serialization.Metadata;
using Jobranger.Messaging.Ads;
using Jobranger.Messaging.Applications;
using Jobranger.Messaging.Learning;
using Jobranger.Messaging.Profiles;
using Jobranger.Messaging.Replies;
using Jobranger.Messaging.Runs;
using Jobranger.Messaging.Settings;

namespace Jobranger.Messaging.Payloads;

internal static class PayloadTypes {
    private static readonly MessagesJsonContext context = MessagesJsonContext.Default;

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
