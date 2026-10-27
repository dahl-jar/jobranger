using System.Text.Json.Serialization;
using Jobranger.Messaging.Ads;
using Jobranger.Messaging.Applications;
using Jobranger.Messaging.Envelopes;
using Jobranger.Messaging.Learning;
using Jobranger.Messaging.Profiles;
using Jobranger.Messaging.Replies;
using Jobranger.Messaging.Runs;
using Jobranger.Messaging.Settings;

namespace Jobranger.Messaging;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(Envelope))]
[JsonSerializable(typeof(Status))]
[JsonSerializable(typeof(AdsSnapshot))]
[JsonSerializable(typeof(ApplicationsSnapshot))]
[JsonSerializable(typeof(ConfigSnapshot))]
[JsonSerializable(typeof(ProfileSnapshot))]
[JsonSerializable(typeof(UsageSnapshot))]
[JsonSerializable(typeof(ModelsSnapshot))]
[JsonSerializable(typeof(GuidanceSnapshot))]
[JsonSerializable(typeof(ContextSnapshot))]
[JsonSerializable(typeof(LearningSnapshot))]
[JsonSerializable(typeof(RunProgress))]
[JsonSerializable(typeof(RunFinished))]
[JsonSerializable(typeof(RunCancelled))]
[JsonSerializable(typeof(Saved))]
[JsonSerializable(typeof(ErrorMessage))]
[JsonSerializable(typeof(Unpaired))]
[JsonSerializable(typeof(RunStart))]
[JsonSerializable(typeof(RunCancel))]
[JsonSerializable(typeof(AdMark))]
[JsonSerializable(typeof(AdUnmark))]
[JsonSerializable(typeof(AdShowAnyway))]
[JsonSerializable(typeof(ApplicationCreate))]
[JsonSerializable(typeof(ApplicationUpdate))]
[JsonSerializable(typeof(ApplicationRemove))]
[JsonSerializable(typeof(ConfigUpdate))]
[JsonSerializable(typeof(ModelSelect))]
[JsonSerializable(typeof(KeyReplace))]
[JsonSerializable(typeof(CvUpload))]
[JsonSerializable(typeof(ProfileUpdate))]
[JsonSerializable(typeof(GuidanceUpdate))]
[JsonSerializable(typeof(GuidanceReset))]
[JsonSerializable(typeof(ContextUpdate))]
[JsonSerializable(typeof(ContextUpload))]
[JsonSerializable(typeof(ContextRemove))]
[JsonSerializable(typeof(LearningForget))]
[JsonSerializable(typeof(LearningForgetAll))]
[JsonSerializable(typeof(Unpair))]
[JsonSerializable(typeof(Chunk))]
public sealed partial class MessagesJsonContext : JsonSerializerContext;
