import * as func1Module from "ajv/dist/runtime/ucs2length";
import * as formats0Module from "ajv-formats/dist/formats";
import * as func0Module from "ajv/dist/runtime/equal";
function resolveExport(namespace, member) {
  const direct = namespace[member];
  if (direct !== undefined && (member !== "default" || typeof direct === "function")) {
    return direct;
  }
  return namespace.default[member];
}

export const validatePayload = validate52;
const schema19 = {"$schema":"https://json-schema.org/draft/2020-12/schema","title":"Payload","type":"object","oneOf":[{"$ref":"#/$defs/Status"},{"$ref":"#/$defs/AdsSnapshot"},{"$ref":"#/$defs/ApplicationsSnapshot"},{"$ref":"#/$defs/ConfigSnapshot"},{"$ref":"#/$defs/ProfileSnapshot"},{"$ref":"#/$defs/UsageSnapshot"},{"$ref":"#/$defs/ModelsSnapshot"},{"$ref":"#/$defs/GuidanceSnapshot"},{"$ref":"#/$defs/ContextSnapshot"},{"$ref":"#/$defs/LearningSnapshot"},{"$ref":"#/$defs/RunProgress"},{"$ref":"#/$defs/RunFinished"},{"$ref":"#/$defs/RunCancelled"},{"$ref":"#/$defs/Saved"},{"$ref":"#/$defs/ErrorMessage"},{"$ref":"#/$defs/Unpaired"},{"$ref":"#/$defs/RunStart"},{"$ref":"#/$defs/RunCancel"},{"$ref":"#/$defs/AdMark"},{"$ref":"#/$defs/AdUnmark"},{"$ref":"#/$defs/AdShowAnyway"},{"$ref":"#/$defs/ApplicationCreate"},{"$ref":"#/$defs/ApplicationUpdate"},{"$ref":"#/$defs/ApplicationRemove"},{"$ref":"#/$defs/ConfigUpdate"},{"$ref":"#/$defs/ModelSelect"},{"$ref":"#/$defs/KeyReplace"},{"$ref":"#/$defs/CvUpload"},{"$ref":"#/$defs/ProfileUpdate"},{"$ref":"#/$defs/GuidanceUpdate"},{"$ref":"#/$defs/GuidanceReset"},{"$ref":"#/$defs/ContextUpdate"},{"$ref":"#/$defs/ContextUpload"},{"$ref":"#/$defs/ContextRemove"},{"$ref":"#/$defs/LearningForget"},{"$ref":"#/$defs/LearningForgetAll"},{"$ref":"#/$defs/Unpair"},{"$ref":"#/$defs/Chunk"}],"discriminator":{"propertyName":"type"},"$defs":{"MessageId":{"type":"string","pattern":"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$","not":{"pattern":"\\s"}},"RunId":{"type":"integer","minimum":1,"maximum":9007199254740991},"AdId":{"type":"string","minLength":1,"maxLength":200},"Company":{"type":"string","minLength":1,"maxLength":200},"AdTitle":{"type":"string","minLength":1,"maxLength":200},"ModelId":{"type":"string","minLength":1,"maxLength":120},"Note":{"type":"string","maxLength":2000},"Terms":{"type":"array","items":{"type":"string","minLength":1,"maxLength":60},"maxItems":40},"ApplicationId":{"type":"integer","minimum":1,"maximum":9007199254740991},"DecisionId":{"type":"integer","minimum":1,"maximum":9007199254740991},"Timestamp":{"type":"string","pattern":"^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,9})?(Z|[+-]([01][0-9]|2[0-3]):[0-5][0-9])$","not":{"pattern":"\\s"}},"Day":{"type":"string","format":"date","not":{"pattern":"^0000-"}},"ClockTime":{"type":"string","pattern":"^([01][0-9]|2[0-3]):[0-5][0-9]$","minLength":5,"maxLength":5},"Money":{"type":"number","minimum":0,"maximum":1000000},"Count":{"type":"integer","minimum":0,"maximum":9007199254740991},"Limit":{"type":"integer","minimum":1,"maximum":9007199254740991},"ShortText":{"type":"string","maxLength":300},"RoleId":{"type":"string","enum":["backend","frontend","fullstack","mobile","data_engineering","devops_platform","ml_ai","embedded","game"]},"RoleType":{"anyOf":[{"$ref":"#/$defs/RoleId"},{"const":"general_software"}]},"SourceId":{"type":"string","enum":["nav","finn","linkedin","teamtailor","ashby"]},"Fit":{"type":"string","enum":["strong","partial","on_request"]},"RunMode":{"type":"string","enum":["scheduled","manual","dry_run"]},"Mark":{"type":"string","enum":["applied","skipped","not_for_me"]},"CheckState":{"type":"string","enum":["met","partly","missing"]},"Check":{"type":"object","required":["text","state"],"properties":{"text":{"type":"string","minLength":1,"maxLength":160},"state":{"$ref":"#/$defs/CheckState"}}},"Level":{"type":"string","enum":["junior","mid","senior","lead","none"]},"YearBand":{"type":"string","enum":["0-1","1-3","3-6","6+"]},"RoleIds":{"type":"array","items":{"$ref":"#/$defs/RoleId"},"uniqueItems":true,"maxItems":9},"Levels":{"type":"array","items":{"$ref":"#/$defs/Level"},"uniqueItems":true,"maxItems":5},"YearBands":{"type":"array","items":{"$ref":"#/$defs/YearBand"},"uniqueItems":true,"maxItems":4},"Places":{"type":"array","items":{"type":"string","minLength":1,"maxLength":80},"uniqueItems":true,"maxItems":20},"ClockTimes":{"type":"array","items":{"$ref":"#/$defs/ClockTime"},"uniqueItems":true,"maxItems":12},"ProviderId":{"type":"string","enum":["anthropic","openai","openrouter","ollama"]},"Language":{"type":"string","enum":["en","nb"]},"ApplicationStatus":{"type":"string","enum":["waiting","interview","offer","accepted","rejected"]},"HiddenKind":{"type":"string","enum":["senior","years","masters","role","place","other"]},"ClosedReason":{"type":"string","enum":["deadline","removed","filled"]},"ClosedMark":{"type":"string","enum":["applied","skipped","not_for_me","none"]},"Base64Data":{"type":"string","pattern":"^[A-Za-z0-9+/]*={0,2}$","not":{"pattern":"[^A-Za-z0-9+/=]"}},"DocId":{"type":"string","pattern":"^doc_[a-z0-9_]+$","maxLength":64,"not":{"pattern":"[^a-z0-9_]"}},"Url":{"type":"string","maxLength":2048,"pattern":"^https?://[\\x21-\\x7e]+$","not":{"pattern":"[^\\x21-\\x7e]|^https?://[^/?#\\\\]*\\\\[^/?#]*@"}},"Email":{"type":"string","maxLength":254,"pattern":"^[A-Za-z0-9!#$%&'*+/=?^_`{|}~.-]+@[A-Za-z0-9-]+(\\.[A-Za-z0-9-]+)*\\.[A-Za-z]+$","not":{"pattern":"[^A-Za-z0-9!#$%&'*+/=?^_`{|}~.@-]"}},"FileName":{"type":"string","minLength":1,"maxLength":120,"pattern":"\\.([pP][dD][fF]|[tT][xX][tT]|[mM][dD])$","not":{"pattern":"[\\u0000-\\u001f\\u007f-\\u009f\\u00ad\\u034f\\u061c\\u115f-\\u1160\\u17b4-\\u17b5\\u180b-\\u180f\\u200b\\u200e-\\u200f\\u2028-\\u202e\\u2060-\\u206f\\u3164\\ufe00-\\ufe0f\\ufeff\\uffa0\\ufff0-\\ufff8\ud82f\udca0-\ud82f\udca3\ud834\udd73-\ud834\udd7a\udb40\udc00-\udb43\udfff/\\\\]"}},"Recheck":{"type":"object","required":["ads","cost_usd"],"properties":{"ads":{"$ref":"#/$defs/Count"},"cost_usd":{"anyOf":[{"$ref":"#/$defs/Money"},{"type":"null"}]}}},"LastRun":{"type":"object","required":["run_id","finished_at","ads_read","matches","cost_usd"],"properties":{"run_id":{"$ref":"#/$defs/RunId"},"finished_at":{"$ref":"#/$defs/Timestamp"},"ads_read":{"$ref":"#/$defs/Count"},"matches":{"$ref":"#/$defs/Count"},"cost_usd":{"$ref":"#/$defs/Money"}}},"ActiveRun":{"type":"object","required":["run_id","mode","started_at","results_by"],"properties":{"run_id":{"$ref":"#/$defs/RunId"},"mode":{"$ref":"#/$defs/RunMode"},"started_at":{"$ref":"#/$defs/Timestamp"},"results_by":{"anyOf":[{"$ref":"#/$defs/Timestamp"},{"type":"null"}]}}},"ActiveAd":{"type":"object","required":["id","company","title","place","role_type","source","published_at","found_at","url","deadline","fit","is_new","today","summary","reason","gaps","checks","mark","marked_at","shown_anyway"],"properties":{"id":{"$ref":"#/$defs/AdId"},"company":{"$ref":"#/$defs/Company"},"title":{"$ref":"#/$defs/AdTitle"},"place":{"type":"string","maxLength":120},"role_type":{"$ref":"#/$defs/RoleType"},"source":{"$ref":"#/$defs/SourceId"},"published_at":{"anyOf":[{"$ref":"#/$defs/Timestamp"},{"type":"null"}]},"found_at":{"$ref":"#/$defs/Timestamp"},"url":{"$ref":"#/$defs/Url"},"deadline":{"anyOf":[{"$ref":"#/$defs/Day"},{"type":"null"}]},"fit":{"$ref":"#/$defs/Fit"},"is_new":{"type":"boolean"},"today":{"type":"boolean"},"summary":{"type":"string","maxLength":700},"reason":{"$ref":"#/$defs/ShortText"},"gaps":{"type":"array","items":{"$ref":"#/$defs/ShortText"},"maxItems":5},"checks":{"type":"array","items":{"$ref":"#/$defs/Check"},"maxItems":5},"mark":{"anyOf":[{"$ref":"#/$defs/Mark"},{"type":"null"}]},"marked_at":{"anyOf":[{"$ref":"#/$defs/Timestamp"},{"type":"null"}]},"shown_anyway":{"type":"boolean"}}},"HiddenAd":{"type":"object","required":["id","company","title","place","role_type","source","url","found_at","today","kind","reason"],"properties":{"id":{"$ref":"#/$defs/AdId"},"company":{"$ref":"#/$defs/Company"},"title":{"$ref":"#/$defs/AdTitle"},"place":{"type":"string","maxLength":120},"role_type":{"anyOf":[{"$ref":"#/$defs/RoleType"},{"type":"null"}]},"source":{"$ref":"#/$defs/SourceId"},"url":{"$ref":"#/$defs/Url"},"found_at":{"$ref":"#/$defs/Timestamp"},"today":{"type":"boolean"},"kind":{"$ref":"#/$defs/HiddenKind"},"reason":{"type":"string","minLength":1,"maxLength":120}}},"HiddenCounts":{"type":"object","required":["senior","years","masters","role","place","other"],"properties":{"senior":{"$ref":"#/$defs/Count"},"years":{"$ref":"#/$defs/Count"},"masters":{"$ref":"#/$defs/Count"},"role":{"$ref":"#/$defs/Count"},"place":{"$ref":"#/$defs/Count"},"other":{"$ref":"#/$defs/Count"}}},"ClosedAd":{"type":"object","required":["id","closed_on","company","title","source","reason","mark"],"properties":{"id":{"$ref":"#/$defs/AdId"},"closed_on":{"$ref":"#/$defs/Day"},"company":{"$ref":"#/$defs/Company"},"title":{"$ref":"#/$defs/AdTitle"},"source":{"$ref":"#/$defs/SourceId"},"reason":{"$ref":"#/$defs/ClosedReason"},"mark":{"$ref":"#/$defs/ClosedMark"}}},"HistoryEntry":{"type":"object","required":["at","text"],"properties":{"at":{"$ref":"#/$defs/Timestamp"},"text":{"type":"string","minLength":1,"maxLength":200}}},"Application":{"type":"object","required":["id","ad_id","company","title","place","role_type","source","url","deadline","fit","status","note","applied_at","found_at","cv_name","checks","history","removed"],"properties":{"id":{"$ref":"#/$defs/ApplicationId"},"ad_id":{"anyOf":[{"$ref":"#/$defs/AdId"},{"type":"null"}]},"company":{"$ref":"#/$defs/Company"},"title":{"type":"string","maxLength":200},"place":{"type":"string","maxLength":120},"role_type":{"anyOf":[{"$ref":"#/$defs/RoleType"},{"type":"null"}]},"source":{"anyOf":[{"$ref":"#/$defs/SourceId"},{"type":"null"}]},"url":{"anyOf":[{"$ref":"#/$defs/Url"},{"type":"null"}]},"deadline":{"anyOf":[{"$ref":"#/$defs/Day"},{"type":"null"}]},"fit":{"anyOf":[{"$ref":"#/$defs/Fit"},{"type":"null"}]},"status":{"$ref":"#/$defs/ApplicationStatus"},"note":{"$ref":"#/$defs/Note"},"applied_at":{"$ref":"#/$defs/Timestamp"},"found_at":{"anyOf":[{"$ref":"#/$defs/Timestamp"},{"type":"null"}]},"cv_name":{"anyOf":[{"type":"string","maxLength":120},{"type":"null"}]},"checks":{"type":"array","items":{"$ref":"#/$defs/Check"},"maxItems":5},"history":{"type":"array","items":{"$ref":"#/$defs/HistoryEntry"},"maxItems":20},"removed":{"type":"boolean"}}},"RoleOption":{"type":"object","required":["id","label"],"properties":{"id":{"$ref":"#/$defs/RoleId"},"label":{"type":"string","minLength":1,"maxLength":40}}},"Targets":{"type":"object","required":["subfields","levels","years","hide_masters","places","relocate"],"properties":{"subfields":{"$ref":"#/$defs/RoleIds"},"levels":{"$ref":"#/$defs/Levels"},"years":{"$ref":"#/$defs/YearBands"},"hide_masters":{"type":"boolean"},"places":{"$ref":"#/$defs/Places"},"relocate":{"type":"boolean"}}},"Sources":{"type":"object","required":["nav","employers","finn","linkedin"],"properties":{"nav":{"type":"boolean"},"employers":{"type":"boolean"},"finn":{"type":"boolean"},"linkedin":{"type":"boolean"}}},"Schedule":{"type":"object","required":["times","min_interval_hours"],"properties":{"times":{"$ref":"#/$defs/ClockTimes"},"min_interval_hours":{"type":"integer","const":2}}},"EmailNotify":{"type":"object","required":["on","address"],"properties":{"on":{"type":"boolean"},"address":{"anyOf":[{"$ref":"#/$defs/Email"},{"type":"null"}]}}},"Notify":{"type":"object","required":["website","email"],"properties":{"website":{"type":"boolean"},"email":{"$ref":"#/$defs/EmailNotify"}}},"Price":{"type":"object","required":["input_per_mtok","output_per_mtok"],"properties":{"input_per_mtok":{"$ref":"#/$defs/Money"},"output_per_mtok":{"$ref":"#/$defs/Money"}}},"ModelConfig":{"type":"object","required":["provider","model","key_hint","price","monthly_limit_usd"],"properties":{"provider":{"anyOf":[{"$ref":"#/$defs/ProviderId"},{"type":"null"}]},"model":{"anyOf":[{"$ref":"#/$defs/ModelId"},{"type":"null"}]},"key_hint":{"anyOf":[{"type":"string","maxLength":4},{"type":"null"}]},"price":{"$ref":"#/$defs/Price"},"monthly_limit_usd":{"anyOf":[{"$ref":"#/$defs/Money"},{"type":"null"}]}}},"TargetsPatch":{"type":"object","properties":{"subfields":{"$ref":"#/$defs/RoleIds"},"levels":{"$ref":"#/$defs/Levels"},"years":{"$ref":"#/$defs/YearBands"},"hide_masters":{"type":"boolean"},"places":{"$ref":"#/$defs/Places"},"relocate":{"type":"boolean"}}},"SourcesPatch":{"type":"object","properties":{"nav":{"type":"boolean"},"employers":{"type":"boolean"},"finn":{"type":"boolean"},"linkedin":{"type":"boolean"}}},"SchedulePatch":{"type":"object","properties":{"times":{"$ref":"#/$defs/ClockTimes"}}},"EmailNotifyPatch":{"type":"object","properties":{"on":{"type":"boolean"},"address":{"anyOf":[{"$ref":"#/$defs/Email"},{"type":"null"}]}}},"NotifyPatch":{"type":"object","properties":{"website":{"type":"boolean"},"email":{"$ref":"#/$defs/EmailNotifyPatch"}}},"PricePatch":{"type":"object","properties":{"input_per_mtok":{"$ref":"#/$defs/Money"},"output_per_mtok":{"$ref":"#/$defs/Money"}}},"ModelConfigPatch":{"type":"object","properties":{"price":{"$ref":"#/$defs/PricePatch"},"monthly_limit_usd":{"anyOf":[{"$ref":"#/$defs/Money"},{"type":"null"}]}}},"ConfigPatch":{"type":"object","properties":{"targets":{"$ref":"#/$defs/TargetsPatch"},"sources":{"$ref":"#/$defs/SourcesPatch"},"schedule":{"$ref":"#/$defs/SchedulePatch"},"notify":{"$ref":"#/$defs/NotifyPatch"},"model":{"$ref":"#/$defs/ModelConfigPatch"},"language":{"$ref":"#/$defs/Language"}}},"ProjectRef":{"type":"object","required":["id","name"],"properties":{"id":{"type":"string","minLength":1,"maxLength":40},"name":{"type":"string","minLength":1,"maxLength":120}}},"ProjectEdit":{"type":"object","required":["id","name"],"properties":{"id":{"anyOf":[{"type":"string","minLength":1,"maxLength":40},{"type":"null"}]},"name":{"type":"string","minLength":1,"maxLength":120}}},"UsageDay":{"type":"object","required":["day","calls","input_tokens","output_tokens","cost_usd","ads_read","ads_checked"],"properties":{"day":{"$ref":"#/$defs/Day"},"calls":{"$ref":"#/$defs/Count"},"input_tokens":{"$ref":"#/$defs/Count"},"output_tokens":{"$ref":"#/$defs/Count"},"cost_usd":{"$ref":"#/$defs/Money"},"ads_read":{"$ref":"#/$defs/Count"},"ads_checked":{"$ref":"#/$defs/Count"}}},"ModelInfo":{"type":"object","required":["id","name","input_per_mtok","output_per_mtok","usd_per_day"],"properties":{"id":{"$ref":"#/$defs/ModelId"},"name":{"type":"string","minLength":1,"maxLength":120},"input_per_mtok":{"anyOf":[{"$ref":"#/$defs/Money"},{"type":"null"}]},"output_per_mtok":{"anyOf":[{"$ref":"#/$defs/Money"},{"type":"null"}]},"usd_per_day":{"anyOf":[{"$ref":"#/$defs/Money"},{"type":"null"}]}}},"ProviderModels":{"type":"object","required":["provider","error","models"],"properties":{"provider":{"$ref":"#/$defs/ProviderId"},"error":{"anyOf":[{"$ref":"#/$defs/ShortText"},{"type":"null"}]},"models":{"type":"array","items":{"$ref":"#/$defs/ModelInfo"},"maxItems":1000}}},"ContextDoc":{"type":"object","required":["id","name","chars","added_at"],"properties":{"id":{"$ref":"#/$defs/DocId"},"name":{"type":"string","minLength":1,"maxLength":120},"chars":{"$ref":"#/$defs/Count"},"added_at":{"$ref":"#/$defs/Day"}}},"LearningExample":{"type":"object","required":["decision_id","ad_id","company","title","decision","at"],"properties":{"decision_id":{"$ref":"#/$defs/DecisionId"},"ad_id":{"$ref":"#/$defs/AdId"},"company":{"$ref":"#/$defs/Company"},"title":{"$ref":"#/$defs/AdTitle"},"decision":{"type":"string","enum":["applied","not_for_me","show_anyway"]},"at":{"$ref":"#/$defs/Timestamp"}}},"SourceStep":{"type":"object","required":["source","state","found_new"],"properties":{"source":{"$ref":"#/$defs/SourceId"},"state":{"type":"string","enum":["pending","active","done","failed"]},"found_new":{"anyOf":[{"$ref":"#/$defs/Count"},{"type":"null"}]}}},"ModelStep":{"type":"object","required":["state","checked","total","results_by"],"properties":{"state":{"type":"string","enum":["pending","active","done","skipped"]},"checked":{"$ref":"#/$defs/Count"},"total":{"$ref":"#/$defs/Count"},"results_by":{"anyOf":[{"$ref":"#/$defs/Timestamp"},{"type":"null"}]}}},"Status":{"type":"object","required":["type","v","cli_version","contract_version","machine","paired_at","last_run","next_run_at","running","budget_paused"],"properties":{"type":{"const":"status"},"v":{"const":1},"cli_version":{"type":"string","minLength":1,"maxLength":32},"contract_version":{"type":"integer","minimum":1,"maximum":9007199254740991},"machine":{"type":"string","minLength":1,"maxLength":64},"paired_at":{"$ref":"#/$defs/Timestamp"},"last_run":{"anyOf":[{"$ref":"#/$defs/LastRun"},{"type":"null"}]},"next_run_at":{"anyOf":[{"$ref":"#/$defs/Timestamp"},{"type":"null"}]},"running":{"anyOf":[{"$ref":"#/$defs/ActiveRun"},{"type":"null"}]},"budget_paused":{"type":"boolean"}}},"AdsSnapshot":{"type":"object","required":["type","v","generated_at","as_of","active","active_total","hidden","hidden_total","hidden_counts","closed","closed_total"],"properties":{"type":{"const":"ads.snapshot"},"v":{"const":1},"generated_at":{"$ref":"#/$defs/Timestamp"},"as_of":{"$ref":"#/$defs/Day"},"active":{"type":"array","items":{"$ref":"#/$defs/ActiveAd"},"maxItems":73},"active_total":{"$ref":"#/$defs/Count"},"hidden":{"type":"array","items":{"$ref":"#/$defs/HiddenAd"},"maxItems":500},"hidden_total":{"$ref":"#/$defs/Count"},"hidden_counts":{"$ref":"#/$defs/HiddenCounts"},"closed":{"type":"array","items":{"$ref":"#/$defs/ClosedAd"},"maxItems":200},"closed_total":{"$ref":"#/$defs/Count"}}},"ApplicationsSnapshot":{"type":"object","required":["type","v","generated_at","applications","applications_total"],"properties":{"type":{"const":"applications.snapshot"},"v":{"const":1},"generated_at":{"$ref":"#/$defs/Timestamp"},"applications":{"type":"array","items":{"$ref":"#/$defs/Application"},"maxItems":145},"applications_total":{"$ref":"#/$defs/Count"}}},"ConfigSnapshot":{"type":"object","required":["type","v","roles","targets","sources","schedule","notify","model","language"],"properties":{"type":{"const":"config.snapshot"},"v":{"const":1},"roles":{"type":"array","items":{"$ref":"#/$defs/RoleOption"},"maxItems":9},"targets":{"$ref":"#/$defs/Targets"},"sources":{"$ref":"#/$defs/Sources"},"schedule":{"$ref":"#/$defs/Schedule"},"notify":{"$ref":"#/$defs/Notify"},"model":{"$ref":"#/$defs/ModelConfig"},"language":{"$ref":"#/$defs/Language"}}},"ProfileSnapshot":{"type":"object","required":["type","v","cv_name","cv_read_at","summary","education","languages","stack","projects"],"properties":{"type":{"const":"profile.snapshot"},"v":{"const":1},"cv_name":{"anyOf":[{"type":"string","maxLength":120},{"type":"null"}]},"cv_read_at":{"anyOf":[{"$ref":"#/$defs/Timestamp"},{"type":"null"}]},"summary":{"$ref":"#/$defs/ShortText"},"education":{"$ref":"#/$defs/ShortText"},"languages":{"$ref":"#/$defs/Terms"},"stack":{"$ref":"#/$defs/Terms"},"projects":{"type":"array","items":{"$ref":"#/$defs/ProjectRef"},"maxItems":20}}},"UsageSnapshot":{"type":"object","required":["type","v","month","month_tokens","today_tokens","month_usd","today_usd","resets_on","days"],"properties":{"type":{"const":"usage.snapshot"},"v":{"const":1},"month":{"type":"string","pattern":"^[0-9]{4}-(0[1-9]|1[0-2])$","not":{"pattern":"^0000-"},"minLength":7,"maxLength":7},"month_tokens":{"$ref":"#/$defs/Count"},"today_tokens":{"$ref":"#/$defs/Count"},"month_usd":{"$ref":"#/$defs/Money"},"today_usd":{"$ref":"#/$defs/Money"},"resets_on":{"$ref":"#/$defs/Day"},"days":{"type":"array","items":{"$ref":"#/$defs/UsageDay"},"maxItems":14}}},"ModelsSnapshot":{"type":"object","required":["type","v","providers"],"properties":{"type":{"const":"models.snapshot"},"v":{"const":1},"providers":{"type":"array","items":{"$ref":"#/$defs/ProviderModels"},"maxItems":4}}},"GuidanceSnapshot":{"type":"object","required":["type","v","text","is_default","limit"],"properties":{"type":{"const":"guidance.snapshot"},"v":{"const":1},"text":{"type":"string","maxLength":12000},"is_default":{"type":"boolean"},"limit":{"$ref":"#/$defs/Limit"}}},"ContextSnapshot":{"type":"object","required":["type","v","about","about_limit","docs","docs_limit"],"properties":{"type":{"const":"context.snapshot"},"v":{"const":1},"about":{"type":"string","maxLength":4000},"about_limit":{"$ref":"#/$defs/Limit"},"docs":{"type":"array","items":{"$ref":"#/$defs/ContextDoc"},"maxItems":10},"docs_limit":{"$ref":"#/$defs/Limit"}}},"LearningSnapshot":{"type":"object","required":["type","v","state","unavailable_reason","decision_count","cold_start_threshold","examples","examples_total"],"properties":{"type":{"const":"learning.snapshot"},"v":{"const":1},"state":{"type":"string","enum":["unavailable","collecting","preparing","on"]},"unavailable_reason":{"anyOf":[{"type":"string","maxLength":200},{"type":"null"}]},"decision_count":{"$ref":"#/$defs/Count"},"cold_start_threshold":{"type":"integer","const":20},"examples":{"type":"array","items":{"$ref":"#/$defs/LearningExample"},"maxItems":200},"examples_total":{"$ref":"#/$defs/Count"}}},"RunProgress":{"type":"object","required":["type","v","run_id","mode","started_at","percent","sources","model"],"properties":{"type":{"const":"run.progress"},"v":{"const":1},"run_id":{"$ref":"#/$defs/RunId"},"mode":{"$ref":"#/$defs/RunMode"},"started_at":{"$ref":"#/$defs/Timestamp"},"percent":{"type":"integer","minimum":0,"maximum":100},"sources":{"type":"array","items":{"$ref":"#/$defs/SourceStep"},"maxItems":5},"model":{"$ref":"#/$defs/ModelStep"}}},"RunFinished":{"type":"object","required":["type","v","run_id","finished_at","ads_read","new_ads","matches","hidden","cost_usd","failed_sources","budget_paused"],"properties":{"type":{"const":"run.finished"},"v":{"const":1},"run_id":{"$ref":"#/$defs/RunId"},"finished_at":{"$ref":"#/$defs/Timestamp"},"ads_read":{"$ref":"#/$defs/Count"},"new_ads":{"$ref":"#/$defs/Count"},"matches":{"$ref":"#/$defs/Count"},"hidden":{"$ref":"#/$defs/Count"},"cost_usd":{"$ref":"#/$defs/Money"},"failed_sources":{"type":"array","items":{"$ref":"#/$defs/SourceId"},"maxItems":5},"budget_paused":{"type":"boolean"}}},"RunCancelled":{"type":"object","required":["type","v","run_id","at"],"properties":{"type":{"const":"run.cancelled"},"v":{"const":1},"run_id":{"$ref":"#/$defs/RunId"},"at":{"$ref":"#/$defs/Timestamp"}}},"Saved":{"type":"object","required":["type","v","ref","applies_from"],"properties":{"type":{"const":"saved"},"v":{"const":1},"ref":{"$ref":"#/$defs/MessageId"},"applies_from":{"type":"string","enum":["now","next_run"]},"recheck":{"$ref":"#/$defs/Recheck"}}},"ErrorMessage":{"type":"object","required":["type","v","ref","code","message","field"],"properties":{"type":{"const":"error"},"v":{"const":1},"ref":{"anyOf":[{"$ref":"#/$defs/MessageId"},{"type":"null"}]},"code":{"type":"string","enum":["invalid_payload","unknown_type","unsupported_version","not_found","run_in_progress","no_run","run_failed","key_rejected","cv_type","cv_unreadable","cv_no_text","cv_too_large","cv_parse_failed","chunk_incomplete","internal","guidance_empty","context_full","context_unreadable"]},"message":{"type":"string","minLength":1,"maxLength":300},"field":{"anyOf":[{"type":"string","maxLength":120},{"type":"null"}]}}},"Unpaired":{"type":"object","required":["type","v","reason"],"properties":{"type":{"const":"unpaired"},"v":{"const":1},"reason":{"type":"string","enum":["refused","web","cli","revoked"]}}},"RunStart":{"type":"object","required":["type","v","id"],"properties":{"type":{"const":"run.start"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"}}},"RunCancel":{"type":"object","required":["type","v","id","run_id"],"properties":{"type":{"const":"run.cancel"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"run_id":{"$ref":"#/$defs/RunId"}}},"AdMark":{"type":"object","required":["type","v","id","ad_id","mark"],"properties":{"type":{"const":"ad.mark"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"ad_id":{"$ref":"#/$defs/AdId"},"mark":{"$ref":"#/$defs/Mark"}}},"AdUnmark":{"type":"object","required":["type","v","id","ad_id"],"properties":{"type":{"const":"ad.unmark"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"ad_id":{"$ref":"#/$defs/AdId"}}},"AdShowAnyway":{"type":"object","required":["type","v","id","ad_id","on"],"properties":{"type":{"const":"ad.show_anyway"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"ad_id":{"$ref":"#/$defs/AdId"},"on":{"type":"boolean"}}},"ApplicationCreate":{"type":"object","required":["type","v","id","company","title","note"],"properties":{"type":{"const":"application.create"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"company":{"$ref":"#/$defs/Company","type":"string","pattern":"[^\\u0000-\\u0020\\u007f-\\u00a0\\u00ad\\u034f\\u061c\\u115f-\\u1160\\u1680\\u17b4-\\u17b5\\u180b-\\u180f\\u2000-\\u200f\\u2028-\\u202f\\u205f-\\u206f\\u2800\\u3000\\u3164\\ufe00-\\ufe0f\\ufeff\\uffa0\\ufff0-\\ufff8\ud82f\udca0-\ud82f\udca3\ud834\udd73-\ud834\udd7a\udb40\udc00-\udb43\udfff]"},"title":{"type":"string","maxLength":200},"note":{"$ref":"#/$defs/Note"}}},"ApplicationUpdate":{"type":"object","required":["type","v","id","application_id"],"properties":{"type":{"const":"application.update"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"application_id":{"$ref":"#/$defs/ApplicationId"},"status":{"$ref":"#/$defs/ApplicationStatus"},"note":{"$ref":"#/$defs/Note"}}},"ApplicationRemove":{"type":"object","required":["type","v","id","application_id","removed"],"properties":{"type":{"const":"application.remove"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"application_id":{"$ref":"#/$defs/ApplicationId"},"removed":{"type":"boolean"}}},"ConfigUpdate":{"type":"object","required":["type","v","id","patch"],"properties":{"type":{"const":"config.update"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"patch":{"$ref":"#/$defs/ConfigPatch"}}},"ModelSelect":{"type":"object","required":["type","v","id","provider","model"],"properties":{"type":{"const":"model.select"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"provider":{"$ref":"#/$defs/ProviderId"},"model":{"$ref":"#/$defs/ModelId"}}},"KeyReplace":{"type":"object","required":["type","v","id","provider","key"],"properties":{"type":{"const":"key.replace"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"provider":{"$ref":"#/$defs/ProviderId"},"key":{"type":"string","pattern":"^[\\x21-\\x7e]{12,512}$","not":{"pattern":"[^\\x21-\\x7e]"}}}},"CvUpload":{"type":"object","required":["type","v","id","name","size_bytes","content_b64"],"properties":{"type":{"const":"cv.upload"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"name":{"$ref":"#/$defs/FileName"},"size_bytes":{"type":"integer","minimum":1,"maximum":5242880},"content_b64":{"$ref":"#/$defs/Base64Data","type":"string","maxLength":6990508}}},"ProfileUpdate":{"type":"object","required":["type","v","id"],"properties":{"type":{"const":"profile.update"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"summary":{"$ref":"#/$defs/ShortText"},"education":{"$ref":"#/$defs/ShortText"},"languages":{"$ref":"#/$defs/Terms"},"stack":{"$ref":"#/$defs/Terms"},"projects":{"type":"array","items":{"$ref":"#/$defs/ProjectEdit"},"maxItems":20}}},"GuidanceUpdate":{"type":"object","required":["type","v","id","text"],"properties":{"type":{"const":"guidance.update"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"text":{"type":"string","maxLength":12000}}},"GuidanceReset":{"type":"object","required":["type","v","id"],"properties":{"type":{"const":"guidance.reset"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"}}},"ContextUpdate":{"type":"object","required":["type","v","id","about"],"properties":{"type":{"const":"context.update"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"about":{"type":"string","maxLength":4000}}},"ContextUpload":{"type":"object","required":["type","v","id","name","content_b64"],"properties":{"type":{"const":"context.upload"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"name":{"$ref":"#/$defs/FileName"},"content_b64":{"$ref":"#/$defs/Base64Data","type":"string","maxLength":6990508}}},"ContextRemove":{"type":"object","required":["type","v","id","doc_id"],"properties":{"type":{"const":"context.remove"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"doc_id":{"$ref":"#/$defs/DocId"}}},"LearningForget":{"type":"object","required":["type","v","id","decision_id"],"properties":{"type":{"const":"learning.forget"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"decision_id":{"$ref":"#/$defs/DecisionId"}}},"LearningForgetAll":{"type":"object","required":["type","v","id"],"properties":{"type":{"const":"learning.forget_all"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"}}},"Unpair":{"type":"object","required":["type","v","id"],"properties":{"type":{"const":"unpair"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"}}},"Chunk":{"type":"object","required":["type","v","chunk_id","index","count","data"],"properties":{"type":{"const":"chunk"},"v":{"const":1},"chunk_id":{"$ref":"#/$defs/MessageId"},"index":{"type":"integer","minimum":0,"maximum":255},"count":{"type":"integer","minimum":1,"maximum":256},"data":{"$ref":"#/$defs/Base64Data","type":"string","maxLength":43692}}}}};
const schema20 = {"type":"object","required":["type","v","cli_version","contract_version","machine","paired_at","last_run","next_run_at","running","budget_paused"],"properties":{"type":{"const":"status"},"v":{"const":1},"cli_version":{"type":"string","minLength":1,"maxLength":32},"contract_version":{"type":"integer","minimum":1,"maximum":9007199254740991},"machine":{"type":"string","minLength":1,"maxLength":64},"paired_at":{"$ref":"#/$defs/Timestamp"},"last_run":{"anyOf":[{"$ref":"#/$defs/LastRun"},{"type":"null"}]},"next_run_at":{"anyOf":[{"$ref":"#/$defs/Timestamp"},{"type":"null"}]},"running":{"anyOf":[{"$ref":"#/$defs/ActiveRun"},{"type":"null"}]},"budget_paused":{"type":"boolean"}}};
const func1 = resolveExport(func1Module, "default");
const schema21 = {"type":"string","pattern":"^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,9})?(Z|[+-]([01][0-9]|2[0-3]):[0-5][0-9])$","not":{"pattern":"\\s"}};
const pattern3 = new RegExp("\\s", "u");
const pattern4 = new RegExp("^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,9})?(Z|[+-]([01][0-9]|2[0-3]):[0-5][0-9])$", "u");

function validate54(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate54.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
const _errs1 = errors;
const _errs2 = errors;
if(typeof data === "string"){
if(!pattern3.test(data)){
const err0 = {};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
}
var valid0 = _errs2 === errors;
if(valid0){
validate54.errors = [{instancePath,schemaPath:"#/not",keyword:"not",params:{},message:"must NOT be valid"}];
return false;
}
else {
errors = _errs1;
if(vErrors !== null){
if(_errs1){
vErrors.length = _errs1;
}
else {
vErrors = null;
}
}
}
if(errors === 0){
if(typeof data === "string"){
if(!pattern4.test(data)){
validate54.errors = [{instancePath,schemaPath:"#/pattern",keyword:"pattern",params:{pattern: "^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,9})?(Z|[+-]([01][0-9]|2[0-3]):[0-5][0-9])$"},message:"must match pattern \""+"^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,9})?(Z|[+-]([01][0-9]|2[0-3]):[0-5][0-9])$"+"\""}];
return false;
}
}
else {
validate54.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
validate54.errors = vErrors;
return errors === 0;
}
validate54.evaluated = {"dynamicProps":false,"dynamicItems":false};

const schema22 = {"type":"object","required":["run_id","finished_at","ads_read","matches","cost_usd"],"properties":{"run_id":{"$ref":"#/$defs/RunId"},"finished_at":{"$ref":"#/$defs/Timestamp"},"ads_read":{"$ref":"#/$defs/Count"},"matches":{"$ref":"#/$defs/Count"},"cost_usd":{"$ref":"#/$defs/Money"}}};
const schema23 = {"type":"integer","minimum":1,"maximum":9007199254740991};

function validate57(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate57.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(!(((typeof data == "number") && (!(data % 1) && !isNaN(data))) && (isFinite(data)))){
validate57.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "integer"},message:"must be integer"}];
return false;
}
if(errors === 0){
if((typeof data == "number") && (isFinite(data))){
if(data > 9007199254740991 || isNaN(data)){
validate57.errors = [{instancePath,schemaPath:"#/maximum",keyword:"maximum",params:{comparison: "<=", limit: 9007199254740991},message:"must be <= 9007199254740991"}];
return false;
}
else {
if(data < 1 || isNaN(data)){
validate57.errors = [{instancePath,schemaPath:"#/minimum",keyword:"minimum",params:{comparison: ">=", limit: 1},message:"must be >= 1"}];
return false;
}
}
}
}
validate57.errors = vErrors;
return errors === 0;
}
validate57.evaluated = {"dynamicProps":false,"dynamicItems":false};

const schema24 = {"type":"integer","minimum":0,"maximum":9007199254740991};

function validate60(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate60.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(!(((typeof data == "number") && (!(data % 1) && !isNaN(data))) && (isFinite(data)))){
validate60.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "integer"},message:"must be integer"}];
return false;
}
if(errors === 0){
if((typeof data == "number") && (isFinite(data))){
if(data > 9007199254740991 || isNaN(data)){
validate60.errors = [{instancePath,schemaPath:"#/maximum",keyword:"maximum",params:{comparison: "<=", limit: 9007199254740991},message:"must be <= 9007199254740991"}];
return false;
}
else {
if(data < 0 || isNaN(data)){
validate60.errors = [{instancePath,schemaPath:"#/minimum",keyword:"minimum",params:{comparison: ">=", limit: 0},message:"must be >= 0"}];
return false;
}
}
}
}
validate60.errors = vErrors;
return errors === 0;
}
validate60.evaluated = {"dynamicProps":false,"dynamicItems":false};

const schema25 = {"type":"number","minimum":0,"maximum":1000000};

function validate63(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate63.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if((typeof data == "number") && (isFinite(data))){
if(data > 1000000 || isNaN(data)){
validate63.errors = [{instancePath,schemaPath:"#/maximum",keyword:"maximum",params:{comparison: "<=", limit: 1000000},message:"must be <= 1000000"}];
return false;
}
else {
if(data < 0 || isNaN(data)){
validate63.errors = [{instancePath,schemaPath:"#/minimum",keyword:"minimum",params:{comparison: ">=", limit: 0},message:"must be >= 0"}];
return false;
}
}
}
else {
validate63.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "number"},message:"must be number"}];
return false;
}
}
validate63.errors = vErrors;
return errors === 0;
}
validate63.evaluated = {"dynamicProps":false,"dynamicItems":false};


function validate56(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate56.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((((data.run_id === undefined) && (missing0 = "run_id")) || ((data.finished_at === undefined) && (missing0 = "finished_at"))) || ((data.ads_read === undefined) && (missing0 = "ads_read"))) || ((data.matches === undefined) && (missing0 = "matches"))) || ((data.cost_usd === undefined) && (missing0 = "cost_usd"))){
validate56.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.run_id !== undefined){
const _errs1 = errors;
if(!(validate57(data.run_id, {instancePath:instancePath+"/run_id",parentData:data,parentDataProperty:"run_id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate57.errors : vErrors.concat(validate57.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.finished_at !== undefined){
const _errs2 = errors;
if(!(validate54(data.finished_at, {instancePath:instancePath+"/finished_at",parentData:data,parentDataProperty:"finished_at",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate54.errors : vErrors.concat(validate54.errors);
errors = vErrors.length;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.ads_read !== undefined){
const _errs3 = errors;
if(!(validate60(data.ads_read, {instancePath:instancePath+"/ads_read",parentData:data,parentDataProperty:"ads_read",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.matches !== undefined){
const _errs4 = errors;
if(!(validate60(data.matches, {instancePath:instancePath+"/matches",parentData:data,parentDataProperty:"matches",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.cost_usd !== undefined){
const _errs5 = errors;
if(!(validate63(data.cost_usd, {instancePath:instancePath+"/cost_usd",parentData:data,parentDataProperty:"cost_usd",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate63.errors : vErrors.concat(validate63.errors);
errors = vErrors.length;
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
else {
validate56.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate56.errors = vErrors;
return errors === 0;
}
validate56.evaluated = {"props":{"run_id":true,"finished_at":true,"ads_read":true,"matches":true,"cost_usd":true},"dynamicProps":false,"dynamicItems":false};

const schema26 = {"type":"object","required":["run_id","mode","started_at","results_by"],"properties":{"run_id":{"$ref":"#/$defs/RunId"},"mode":{"$ref":"#/$defs/RunMode"},"started_at":{"$ref":"#/$defs/Timestamp"},"results_by":{"anyOf":[{"$ref":"#/$defs/Timestamp"},{"type":"null"}]}}};
const schema27 = {"type":"string","enum":["scheduled","manual","dry_run"]};

function validate69(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate69.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(typeof data !== "string"){
validate69.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
if(!(((data === "scheduled") || (data === "manual")) || (data === "dry_run"))){
validate69.errors = [{instancePath,schemaPath:"#/enum",keyword:"enum",params:{allowedValues: schema27.enum},message:"must be equal to one of the allowed values"}];
return false;
}
validate69.errors = vErrors;
return errors === 0;
}
validate69.evaluated = {"dynamicProps":false,"dynamicItems":false};


function validate67(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate67.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((data.run_id === undefined) && (missing0 = "run_id")) || ((data.mode === undefined) && (missing0 = "mode"))) || ((data.started_at === undefined) && (missing0 = "started_at"))) || ((data.results_by === undefined) && (missing0 = "results_by"))){
validate67.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.run_id !== undefined){
const _errs1 = errors;
if(!(validate57(data.run_id, {instancePath:instancePath+"/run_id",parentData:data,parentDataProperty:"run_id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate57.errors : vErrors.concat(validate57.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.mode !== undefined){
const _errs2 = errors;
if(!(validate69(data.mode, {instancePath:instancePath+"/mode",parentData:data,parentDataProperty:"mode",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate69.errors : vErrors.concat(validate69.errors);
errors = vErrors.length;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.started_at !== undefined){
const _errs3 = errors;
if(!(validate54(data.started_at, {instancePath:instancePath+"/started_at",parentData:data,parentDataProperty:"started_at",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate54.errors : vErrors.concat(validate54.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.results_by !== undefined){
let data3 = data.results_by;
const _errs4 = errors;
const _errs5 = errors;
let valid1 = false;
const _errs6 = errors;
if(!(validate54(data3, {instancePath:instancePath+"/results_by",parentData:data,parentDataProperty:"results_by",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate54.errors : vErrors.concat(validate54.errors);
errors = vErrors.length;
}
var _valid0 = _errs6 === errors;
valid1 = valid1 || _valid0;
const _errs7 = errors;
if(data3 !== null){
const err0 = {instancePath:instancePath+"/results_by",schemaPath:"#/properties/results_by/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
var _valid0 = _errs7 === errors;
valid1 = valid1 || _valid0;
if(!valid1){
const err1 = {instancePath:instancePath+"/results_by",schemaPath:"#/properties/results_by/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
validate67.errors = vErrors;
return false;
}
else {
errors = _errs5;
if(vErrors !== null){
if(_errs5){
vErrors.length = _errs5;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
else {
validate67.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate67.errors = vErrors;
return errors === 0;
}
validate67.evaluated = {"props":{"run_id":true,"mode":true,"started_at":true,"results_by":true},"dynamicProps":false,"dynamicItems":false};


function validate53(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate53.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((((((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.cli_version === undefined) && (missing0 = "cli_version"))) || ((data.contract_version === undefined) && (missing0 = "contract_version"))) || ((data.machine === undefined) && (missing0 = "machine"))) || ((data.paired_at === undefined) && (missing0 = "paired_at"))) || ((data.last_run === undefined) && (missing0 = "last_run"))) || ((data.next_run_at === undefined) && (missing0 = "next_run_at"))) || ((data.running === undefined) && (missing0 = "running"))) || ((data.budget_paused === undefined) && (missing0 = "budget_paused"))){
validate53.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("status" !== data.type){
validate53.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "status"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate53.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.cli_version !== undefined){
let data2 = data.cli_version;
const _errs3 = errors;
if(errors === _errs3){
if(typeof data2 === "string"){
if(func1(data2) > 32){
validate53.errors = [{instancePath:instancePath+"/cli_version",schemaPath:"#/properties/cli_version/maxLength",keyword:"maxLength",params:{limit: 32},message:"must NOT have more than 32 characters"}];
return false;
}
else {
if(func1(data2) < 1){
validate53.errors = [{instancePath:instancePath+"/cli_version",schemaPath:"#/properties/cli_version/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"}];
return false;
}
}
}
else {
validate53.errors = [{instancePath:instancePath+"/cli_version",schemaPath:"#/properties/cli_version/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.contract_version !== undefined){
let data3 = data.contract_version;
const _errs5 = errors;
if(!(((typeof data3 == "number") && (!(data3 % 1) && !isNaN(data3))) && (isFinite(data3)))){
validate53.errors = [{instancePath:instancePath+"/contract_version",schemaPath:"#/properties/contract_version/type",keyword:"type",params:{type: "integer"},message:"must be integer"}];
return false;
}
if(errors === _errs5){
if((typeof data3 == "number") && (isFinite(data3))){
if(data3 > 9007199254740991 || isNaN(data3)){
validate53.errors = [{instancePath:instancePath+"/contract_version",schemaPath:"#/properties/contract_version/maximum",keyword:"maximum",params:{comparison: "<=", limit: 9007199254740991},message:"must be <= 9007199254740991"}];
return false;
}
else {
if(data3 < 1 || isNaN(data3)){
validate53.errors = [{instancePath:instancePath+"/contract_version",schemaPath:"#/properties/contract_version/minimum",keyword:"minimum",params:{comparison: ">=", limit: 1},message:"must be >= 1"}];
return false;
}
}
}
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.machine !== undefined){
let data4 = data.machine;
const _errs7 = errors;
if(errors === _errs7){
if(typeof data4 === "string"){
if(func1(data4) > 64){
validate53.errors = [{instancePath:instancePath+"/machine",schemaPath:"#/properties/machine/maxLength",keyword:"maxLength",params:{limit: 64},message:"must NOT have more than 64 characters"}];
return false;
}
else {
if(func1(data4) < 1){
validate53.errors = [{instancePath:instancePath+"/machine",schemaPath:"#/properties/machine/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"}];
return false;
}
}
}
else {
validate53.errors = [{instancePath:instancePath+"/machine",schemaPath:"#/properties/machine/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs7 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.paired_at !== undefined){
const _errs9 = errors;
if(!(validate54(data.paired_at, {instancePath:instancePath+"/paired_at",parentData:data,parentDataProperty:"paired_at",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate54.errors : vErrors.concat(validate54.errors);
errors = vErrors.length;
}
var valid0 = _errs9 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.last_run !== undefined){
let data6 = data.last_run;
const _errs10 = errors;
const _errs11 = errors;
let valid1 = false;
const _errs12 = errors;
if(!(validate56(data6, {instancePath:instancePath+"/last_run",parentData:data,parentDataProperty:"last_run",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate56.errors : vErrors.concat(validate56.errors);
errors = vErrors.length;
}
var _valid0 = _errs12 === errors;
valid1 = valid1 || _valid0;
if(_valid0){
var props0 = {};
props0.run_id = true;
props0.finished_at = true;
props0.ads_read = true;
props0.matches = true;
props0.cost_usd = true;
}
const _errs13 = errors;
if(data6 !== null){
const err0 = {instancePath:instancePath+"/last_run",schemaPath:"#/properties/last_run/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
var _valid0 = _errs13 === errors;
valid1 = valid1 || _valid0;
if(!valid1){
const err1 = {instancePath:instancePath+"/last_run",schemaPath:"#/properties/last_run/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
validate53.errors = vErrors;
return false;
}
else {
errors = _errs11;
if(vErrors !== null){
if(_errs11){
vErrors.length = _errs11;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs10 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.next_run_at !== undefined){
let data7 = data.next_run_at;
const _errs15 = errors;
const _errs16 = errors;
let valid2 = false;
const _errs17 = errors;
if(!(validate54(data7, {instancePath:instancePath+"/next_run_at",parentData:data,parentDataProperty:"next_run_at",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate54.errors : vErrors.concat(validate54.errors);
errors = vErrors.length;
}
var _valid1 = _errs17 === errors;
valid2 = valid2 || _valid1;
const _errs18 = errors;
if(data7 !== null){
const err2 = {instancePath:instancePath+"/next_run_at",schemaPath:"#/properties/next_run_at/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err2];
}
else {
vErrors.push(err2);
}
errors++;
}
var _valid1 = _errs18 === errors;
valid2 = valid2 || _valid1;
if(!valid2){
const err3 = {instancePath:instancePath+"/next_run_at",schemaPath:"#/properties/next_run_at/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err3];
}
else {
vErrors.push(err3);
}
errors++;
validate53.errors = vErrors;
return false;
}
else {
errors = _errs16;
if(vErrors !== null){
if(_errs16){
vErrors.length = _errs16;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs15 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.running !== undefined){
let data8 = data.running;
const _errs20 = errors;
const _errs21 = errors;
let valid3 = false;
const _errs22 = errors;
if(!(validate67(data8, {instancePath:instancePath+"/running",parentData:data,parentDataProperty:"running",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate67.errors : vErrors.concat(validate67.errors);
errors = vErrors.length;
}
var _valid2 = _errs22 === errors;
valid3 = valid3 || _valid2;
if(_valid2){
var props1 = {};
props1.run_id = true;
props1.mode = true;
props1.started_at = true;
props1.results_by = true;
}
const _errs23 = errors;
if(data8 !== null){
const err4 = {instancePath:instancePath+"/running",schemaPath:"#/properties/running/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err4];
}
else {
vErrors.push(err4);
}
errors++;
}
var _valid2 = _errs23 === errors;
valid3 = valid3 || _valid2;
if(!valid3){
const err5 = {instancePath:instancePath+"/running",schemaPath:"#/properties/running/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err5];
}
else {
vErrors.push(err5);
}
errors++;
validate53.errors = vErrors;
return false;
}
else {
errors = _errs21;
if(vErrors !== null){
if(_errs21){
vErrors.length = _errs21;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs20 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.budget_paused !== undefined){
const _errs25 = errors;
if(typeof data.budget_paused !== "boolean"){
validate53.errors = [{instancePath:instancePath+"/budget_paused",schemaPath:"#/properties/budget_paused/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs25 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
}
}
}
}
else {
validate53.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate53.errors = vErrors;
return errors === 0;
}
validate53.evaluated = {"props":{"type":true,"v":true,"cli_version":true,"contract_version":true,"machine":true,"paired_at":true,"last_run":true,"next_run_at":true,"running":true,"budget_paused":true},"dynamicProps":false,"dynamicItems":false};

const schema28 = {"type":"object","required":["type","v","generated_at","as_of","active","active_total","hidden","hidden_total","hidden_counts","closed","closed_total"],"properties":{"type":{"const":"ads.snapshot"},"v":{"const":1},"generated_at":{"$ref":"#/$defs/Timestamp"},"as_of":{"$ref":"#/$defs/Day"},"active":{"type":"array","items":{"$ref":"#/$defs/ActiveAd"},"maxItems":73},"active_total":{"$ref":"#/$defs/Count"},"hidden":{"type":"array","items":{"$ref":"#/$defs/HiddenAd"},"maxItems":500},"hidden_total":{"$ref":"#/$defs/Count"},"hidden_counts":{"$ref":"#/$defs/HiddenCounts"},"closed":{"type":"array","items":{"$ref":"#/$defs/ClosedAd"},"maxItems":200},"closed_total":{"$ref":"#/$defs/Count"}}};
const schema29 = {"type":"string","format":"date","not":{"pattern":"^0000-"}};
const pattern5 = new RegExp("^0000-", "u");
const formats0 = resolveExport(formats0Module, "fullFormats").date;

function validate76(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate76.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
const _errs1 = errors;
const _errs2 = errors;
if(typeof data === "string"){
if(!pattern5.test(data)){
const err0 = {};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
}
var valid0 = _errs2 === errors;
if(valid0){
validate76.errors = [{instancePath,schemaPath:"#/not",keyword:"not",params:{},message:"must NOT be valid"}];
return false;
}
else {
errors = _errs1;
if(vErrors !== null){
if(_errs1){
vErrors.length = _errs1;
}
else {
vErrors = null;
}
}
}
if(errors === 0){
if(errors === 0){
if(typeof data === "string"){
if(!(formats0.validate(data))){
validate76.errors = [{instancePath,schemaPath:"#/format",keyword:"format",params:{format: "date"},message:"must match format \""+"date"+"\""}];
return false;
}
}
else {
validate76.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
}
validate76.errors = vErrors;
return errors === 0;
}
validate76.evaluated = {"dynamicProps":false,"dynamicItems":false};

const schema30 = {"type":"object","required":["id","company","title","place","role_type","source","published_at","found_at","url","deadline","fit","is_new","today","summary","reason","gaps","checks","mark","marked_at","shown_anyway"],"properties":{"id":{"$ref":"#/$defs/AdId"},"company":{"$ref":"#/$defs/Company"},"title":{"$ref":"#/$defs/AdTitle"},"place":{"type":"string","maxLength":120},"role_type":{"$ref":"#/$defs/RoleType"},"source":{"$ref":"#/$defs/SourceId"},"published_at":{"anyOf":[{"$ref":"#/$defs/Timestamp"},{"type":"null"}]},"found_at":{"$ref":"#/$defs/Timestamp"},"url":{"$ref":"#/$defs/Url"},"deadline":{"anyOf":[{"$ref":"#/$defs/Day"},{"type":"null"}]},"fit":{"$ref":"#/$defs/Fit"},"is_new":{"type":"boolean"},"today":{"type":"boolean"},"summary":{"type":"string","maxLength":700},"reason":{"$ref":"#/$defs/ShortText"},"gaps":{"type":"array","items":{"$ref":"#/$defs/ShortText"},"maxItems":5},"checks":{"type":"array","items":{"$ref":"#/$defs/Check"},"maxItems":5},"mark":{"anyOf":[{"$ref":"#/$defs/Mark"},{"type":"null"}]},"marked_at":{"anyOf":[{"$ref":"#/$defs/Timestamp"},{"type":"null"}]},"shown_anyway":{"type":"boolean"}}};
const schema31 = {"type":"string","minLength":1,"maxLength":200};

function validate79(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate79.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(typeof data === "string"){
if(func1(data) > 200){
validate79.errors = [{instancePath,schemaPath:"#/maxLength",keyword:"maxLength",params:{limit: 200},message:"must NOT have more than 200 characters"}];
return false;
}
else {
if(func1(data) < 1){
validate79.errors = [{instancePath,schemaPath:"#/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"}];
return false;
}
}
}
else {
validate79.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
validate79.errors = vErrors;
return errors === 0;
}
validate79.evaluated = {"dynamicProps":false,"dynamicItems":false};

const schema32 = {"type":"string","minLength":1,"maxLength":200};

function validate81(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate81.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(typeof data === "string"){
if(func1(data) > 200){
validate81.errors = [{instancePath,schemaPath:"#/maxLength",keyword:"maxLength",params:{limit: 200},message:"must NOT have more than 200 characters"}];
return false;
}
else {
if(func1(data) < 1){
validate81.errors = [{instancePath,schemaPath:"#/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"}];
return false;
}
}
}
else {
validate81.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
validate81.errors = vErrors;
return errors === 0;
}
validate81.evaluated = {"dynamicProps":false,"dynamicItems":false};

const schema33 = {"type":"string","minLength":1,"maxLength":200};

function validate83(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate83.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(typeof data === "string"){
if(func1(data) > 200){
validate83.errors = [{instancePath,schemaPath:"#/maxLength",keyword:"maxLength",params:{limit: 200},message:"must NOT have more than 200 characters"}];
return false;
}
else {
if(func1(data) < 1){
validate83.errors = [{instancePath,schemaPath:"#/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"}];
return false;
}
}
}
else {
validate83.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
validate83.errors = vErrors;
return errors === 0;
}
validate83.evaluated = {"dynamicProps":false,"dynamicItems":false};

const schema34 = {"anyOf":[{"$ref":"#/$defs/RoleId"},{"const":"general_software"}]};
const schema35 = {"type":"string","enum":["backend","frontend","fullstack","mobile","data_engineering","devops_platform","ml_ai","embedded","game"]};

function validate86(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate86.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(typeof data !== "string"){
validate86.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
if(!(((((((((data === "backend") || (data === "frontend")) || (data === "fullstack")) || (data === "mobile")) || (data === "data_engineering")) || (data === "devops_platform")) || (data === "ml_ai")) || (data === "embedded")) || (data === "game"))){
validate86.errors = [{instancePath,schemaPath:"#/enum",keyword:"enum",params:{allowedValues: schema35.enum},message:"must be equal to one of the allowed values"}];
return false;
}
validate86.errors = vErrors;
return errors === 0;
}
validate86.evaluated = {"dynamicProps":false,"dynamicItems":false};


function validate85(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate85.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
const _errs0 = errors;
let valid0 = false;
const _errs1 = errors;
if(!(validate86(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate86.errors : vErrors.concat(validate86.errors);
errors = vErrors.length;
}
var _valid0 = _errs1 === errors;
valid0 = valid0 || _valid0;
const _errs2 = errors;
if("general_software" !== data){
const err0 = {instancePath,schemaPath:"#/anyOf/1/const",keyword:"const",params:{allowedValue: "general_software"},message:"must be equal to constant"};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
var _valid0 = _errs2 === errors;
valid0 = valid0 || _valid0;
if(!valid0){
const err1 = {instancePath,schemaPath:"#/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
validate85.errors = vErrors;
return false;
}
else {
errors = _errs0;
if(vErrors !== null){
if(_errs0){
vErrors.length = _errs0;
}
else {
vErrors = null;
}
}
}
validate85.errors = vErrors;
return errors === 0;
}
validate85.evaluated = {"dynamicProps":false,"dynamicItems":false};

const schema36 = {"type":"string","enum":["nav","finn","linkedin","teamtailor","ashby"]};

function validate89(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate89.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(typeof data !== "string"){
validate89.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
if(!(((((data === "nav") || (data === "finn")) || (data === "linkedin")) || (data === "teamtailor")) || (data === "ashby"))){
validate89.errors = [{instancePath,schemaPath:"#/enum",keyword:"enum",params:{allowedValues: schema36.enum},message:"must be equal to one of the allowed values"}];
return false;
}
validate89.errors = vErrors;
return errors === 0;
}
validate89.evaluated = {"dynamicProps":false,"dynamicItems":false};

const schema37 = {"type":"string","maxLength":2048,"pattern":"^https?://[\\x21-\\x7e]+$","not":{"pattern":"[^\\x21-\\x7e]|^https?://[^/?#\\\\]*\\\\[^/?#]*@"}};
const pattern6 = new RegExp("[^\\x21-\\x7e]|^https?://[^/?#\\\\]*\\\\[^/?#]*@", "u");
const pattern7 = new RegExp("^https?://[\\x21-\\x7e]+$", "u");

function validate93(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate93.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
const _errs1 = errors;
const _errs2 = errors;
if(typeof data === "string"){
if(!pattern6.test(data)){
const err0 = {};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
}
var valid0 = _errs2 === errors;
if(valid0){
validate93.errors = [{instancePath,schemaPath:"#/not",keyword:"not",params:{},message:"must NOT be valid"}];
return false;
}
else {
errors = _errs1;
if(vErrors !== null){
if(_errs1){
vErrors.length = _errs1;
}
else {
vErrors = null;
}
}
}
if(errors === 0){
if(typeof data === "string"){
if(func1(data) > 2048){
validate93.errors = [{instancePath,schemaPath:"#/maxLength",keyword:"maxLength",params:{limit: 2048},message:"must NOT have more than 2048 characters"}];
return false;
}
else {
if(!pattern7.test(data)){
validate93.errors = [{instancePath,schemaPath:"#/pattern",keyword:"pattern",params:{pattern: "^https?://[\\x21-\\x7e]+$"},message:"must match pattern \""+"^https?://[\\x21-\\x7e]+$"+"\""}];
return false;
}
}
}
else {
validate93.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
validate93.errors = vErrors;
return errors === 0;
}
validate93.evaluated = {"dynamicProps":false,"dynamicItems":false};

const schema38 = {"type":"string","enum":["strong","partial","on_request"]};

function validate96(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate96.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(typeof data !== "string"){
validate96.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
if(!(((data === "strong") || (data === "partial")) || (data === "on_request"))){
validate96.errors = [{instancePath,schemaPath:"#/enum",keyword:"enum",params:{allowedValues: schema38.enum},message:"must be equal to one of the allowed values"}];
return false;
}
validate96.errors = vErrors;
return errors === 0;
}
validate96.evaluated = {"dynamicProps":false,"dynamicItems":false};

const schema39 = {"type":"string","maxLength":300};

function validate98(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate98.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(typeof data === "string"){
if(func1(data) > 300){
validate98.errors = [{instancePath,schemaPath:"#/maxLength",keyword:"maxLength",params:{limit: 300},message:"must NOT have more than 300 characters"}];
return false;
}
}
else {
validate98.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
validate98.errors = vErrors;
return errors === 0;
}
validate98.evaluated = {"dynamicProps":false,"dynamicItems":false};

const schema40 = {"type":"object","required":["text","state"],"properties":{"text":{"type":"string","minLength":1,"maxLength":160},"state":{"$ref":"#/$defs/CheckState"}}};
const schema41 = {"type":"string","enum":["met","partly","missing"]};

function validate102(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate102.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(typeof data !== "string"){
validate102.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
if(!(((data === "met") || (data === "partly")) || (data === "missing"))){
validate102.errors = [{instancePath,schemaPath:"#/enum",keyword:"enum",params:{allowedValues: schema41.enum},message:"must be equal to one of the allowed values"}];
return false;
}
validate102.errors = vErrors;
return errors === 0;
}
validate102.evaluated = {"dynamicProps":false,"dynamicItems":false};


function validate101(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate101.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((data.text === undefined) && (missing0 = "text")) || ((data.state === undefined) && (missing0 = "state"))){
validate101.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.text !== undefined){
let data0 = data.text;
const _errs1 = errors;
if(errors === _errs1){
if(typeof data0 === "string"){
if(func1(data0) > 160){
validate101.errors = [{instancePath:instancePath+"/text",schemaPath:"#/properties/text/maxLength",keyword:"maxLength",params:{limit: 160},message:"must NOT have more than 160 characters"}];
return false;
}
else {
if(func1(data0) < 1){
validate101.errors = [{instancePath:instancePath+"/text",schemaPath:"#/properties/text/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"}];
return false;
}
}
}
else {
validate101.errors = [{instancePath:instancePath+"/text",schemaPath:"#/properties/text/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.state !== undefined){
const _errs3 = errors;
if(!(validate102(data.state, {instancePath:instancePath+"/state",parentData:data,parentDataProperty:"state",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate102.errors : vErrors.concat(validate102.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
}
}
}
else {
validate101.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate101.errors = vErrors;
return errors === 0;
}
validate101.evaluated = {"props":{"text":true,"state":true},"dynamicProps":false,"dynamicItems":false};

const schema42 = {"type":"string","enum":["applied","skipped","not_for_me"]};

function validate105(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate105.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(typeof data !== "string"){
validate105.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
if(!(((data === "applied") || (data === "skipped")) || (data === "not_for_me"))){
validate105.errors = [{instancePath,schemaPath:"#/enum",keyword:"enum",params:{allowedValues: schema42.enum},message:"must be equal to one of the allowed values"}];
return false;
}
validate105.errors = vErrors;
return errors === 0;
}
validate105.evaluated = {"dynamicProps":false,"dynamicItems":false};


function validate78(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate78.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((((((((((((((((((data.id === undefined) && (missing0 = "id")) || ((data.company === undefined) && (missing0 = "company"))) || ((data.title === undefined) && (missing0 = "title"))) || ((data.place === undefined) && (missing0 = "place"))) || ((data.role_type === undefined) && (missing0 = "role_type"))) || ((data.source === undefined) && (missing0 = "source"))) || ((data.published_at === undefined) && (missing0 = "published_at"))) || ((data.found_at === undefined) && (missing0 = "found_at"))) || ((data.url === undefined) && (missing0 = "url"))) || ((data.deadline === undefined) && (missing0 = "deadline"))) || ((data.fit === undefined) && (missing0 = "fit"))) || ((data.is_new === undefined) && (missing0 = "is_new"))) || ((data.today === undefined) && (missing0 = "today"))) || ((data.summary === undefined) && (missing0 = "summary"))) || ((data.reason === undefined) && (missing0 = "reason"))) || ((data.gaps === undefined) && (missing0 = "gaps"))) || ((data.checks === undefined) && (missing0 = "checks"))) || ((data.mark === undefined) && (missing0 = "mark"))) || ((data.marked_at === undefined) && (missing0 = "marked_at"))) || ((data.shown_anyway === undefined) && (missing0 = "shown_anyway"))){
validate78.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.id !== undefined){
const _errs1 = errors;
if(!(validate79(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate79.errors : vErrors.concat(validate79.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.company !== undefined){
const _errs2 = errors;
if(!(validate81(data.company, {instancePath:instancePath+"/company",parentData:data,parentDataProperty:"company",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate81.errors : vErrors.concat(validate81.errors);
errors = vErrors.length;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.title !== undefined){
const _errs3 = errors;
if(!(validate83(data.title, {instancePath:instancePath+"/title",parentData:data,parentDataProperty:"title",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate83.errors : vErrors.concat(validate83.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.place !== undefined){
let data3 = data.place;
const _errs4 = errors;
if(errors === _errs4){
if(typeof data3 === "string"){
if(func1(data3) > 120){
validate78.errors = [{instancePath:instancePath+"/place",schemaPath:"#/properties/place/maxLength",keyword:"maxLength",params:{limit: 120},message:"must NOT have more than 120 characters"}];
return false;
}
}
else {
validate78.errors = [{instancePath:instancePath+"/place",schemaPath:"#/properties/place/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.role_type !== undefined){
const _errs6 = errors;
if(!(validate85(data.role_type, {instancePath:instancePath+"/role_type",parentData:data,parentDataProperty:"role_type",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate85.errors : vErrors.concat(validate85.errors);
errors = vErrors.length;
}
var valid0 = _errs6 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.source !== undefined){
const _errs7 = errors;
if(!(validate89(data.source, {instancePath:instancePath+"/source",parentData:data,parentDataProperty:"source",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate89.errors : vErrors.concat(validate89.errors);
errors = vErrors.length;
}
var valid0 = _errs7 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.published_at !== undefined){
let data6 = data.published_at;
const _errs8 = errors;
const _errs9 = errors;
let valid1 = false;
const _errs10 = errors;
if(!(validate54(data6, {instancePath:instancePath+"/published_at",parentData:data,parentDataProperty:"published_at",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate54.errors : vErrors.concat(validate54.errors);
errors = vErrors.length;
}
var _valid0 = _errs10 === errors;
valid1 = valid1 || _valid0;
const _errs11 = errors;
if(data6 !== null){
const err0 = {instancePath:instancePath+"/published_at",schemaPath:"#/properties/published_at/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
var _valid0 = _errs11 === errors;
valid1 = valid1 || _valid0;
if(!valid1){
const err1 = {instancePath:instancePath+"/published_at",schemaPath:"#/properties/published_at/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
validate78.errors = vErrors;
return false;
}
else {
errors = _errs9;
if(vErrors !== null){
if(_errs9){
vErrors.length = _errs9;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs8 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.found_at !== undefined){
const _errs13 = errors;
if(!(validate54(data.found_at, {instancePath:instancePath+"/found_at",parentData:data,parentDataProperty:"found_at",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate54.errors : vErrors.concat(validate54.errors);
errors = vErrors.length;
}
var valid0 = _errs13 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.url !== undefined){
const _errs14 = errors;
if(!(validate93(data.url, {instancePath:instancePath+"/url",parentData:data,parentDataProperty:"url",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate93.errors : vErrors.concat(validate93.errors);
errors = vErrors.length;
}
var valid0 = _errs14 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.deadline !== undefined){
let data9 = data.deadline;
const _errs15 = errors;
const _errs16 = errors;
let valid2 = false;
const _errs17 = errors;
if(!(validate76(data9, {instancePath:instancePath+"/deadline",parentData:data,parentDataProperty:"deadline",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate76.errors : vErrors.concat(validate76.errors);
errors = vErrors.length;
}
var _valid1 = _errs17 === errors;
valid2 = valid2 || _valid1;
const _errs18 = errors;
if(data9 !== null){
const err2 = {instancePath:instancePath+"/deadline",schemaPath:"#/properties/deadline/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err2];
}
else {
vErrors.push(err2);
}
errors++;
}
var _valid1 = _errs18 === errors;
valid2 = valid2 || _valid1;
if(!valid2){
const err3 = {instancePath:instancePath+"/deadline",schemaPath:"#/properties/deadline/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err3];
}
else {
vErrors.push(err3);
}
errors++;
validate78.errors = vErrors;
return false;
}
else {
errors = _errs16;
if(vErrors !== null){
if(_errs16){
vErrors.length = _errs16;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs15 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.fit !== undefined){
const _errs20 = errors;
if(!(validate96(data.fit, {instancePath:instancePath+"/fit",parentData:data,parentDataProperty:"fit",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate96.errors : vErrors.concat(validate96.errors);
errors = vErrors.length;
}
var valid0 = _errs20 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.is_new !== undefined){
const _errs21 = errors;
if(typeof data.is_new !== "boolean"){
validate78.errors = [{instancePath:instancePath+"/is_new",schemaPath:"#/properties/is_new/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs21 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.today !== undefined){
const _errs23 = errors;
if(typeof data.today !== "boolean"){
validate78.errors = [{instancePath:instancePath+"/today",schemaPath:"#/properties/today/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs23 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.summary !== undefined){
let data13 = data.summary;
const _errs25 = errors;
if(errors === _errs25){
if(typeof data13 === "string"){
if(func1(data13) > 700){
validate78.errors = [{instancePath:instancePath+"/summary",schemaPath:"#/properties/summary/maxLength",keyword:"maxLength",params:{limit: 700},message:"must NOT have more than 700 characters"}];
return false;
}
}
else {
validate78.errors = [{instancePath:instancePath+"/summary",schemaPath:"#/properties/summary/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs25 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.reason !== undefined){
const _errs27 = errors;
if(!(validate98(data.reason, {instancePath:instancePath+"/reason",parentData:data,parentDataProperty:"reason",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate98.errors : vErrors.concat(validate98.errors);
errors = vErrors.length;
}
var valid0 = _errs27 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.gaps !== undefined){
let data15 = data.gaps;
const _errs28 = errors;
if(errors === _errs28){
if(Array.isArray(data15)){
if(data15.length > 5){
validate78.errors = [{instancePath:instancePath+"/gaps",schemaPath:"#/properties/gaps/maxItems",keyword:"maxItems",params:{limit: 5},message:"must NOT have more than 5 items"}];
return false;
}
else {
var valid3 = true;
const len0 = data15.length;
for(let i0=0; i0<len0; i0++){
const _errs30 = errors;
if(!(validate98(data15[i0], {instancePath:instancePath+"/gaps/" + i0,parentData:data15,parentDataProperty:i0,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate98.errors : vErrors.concat(validate98.errors);
errors = vErrors.length;
}
var valid3 = _errs30 === errors;
if(!valid3){
break;
}
}
}
}
else {
validate78.errors = [{instancePath:instancePath+"/gaps",schemaPath:"#/properties/gaps/type",keyword:"type",params:{type: "array"},message:"must be array"}];
return false;
}
}
var valid0 = _errs28 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.checks !== undefined){
let data17 = data.checks;
const _errs31 = errors;
if(errors === _errs31){
if(Array.isArray(data17)){
if(data17.length > 5){
validate78.errors = [{instancePath:instancePath+"/checks",schemaPath:"#/properties/checks/maxItems",keyword:"maxItems",params:{limit: 5},message:"must NOT have more than 5 items"}];
return false;
}
else {
var valid4 = true;
const len1 = data17.length;
for(let i1=0; i1<len1; i1++){
const _errs33 = errors;
if(!(validate101(data17[i1], {instancePath:instancePath+"/checks/" + i1,parentData:data17,parentDataProperty:i1,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate101.errors : vErrors.concat(validate101.errors);
errors = vErrors.length;
}
var valid4 = _errs33 === errors;
if(!valid4){
break;
}
}
}
}
else {
validate78.errors = [{instancePath:instancePath+"/checks",schemaPath:"#/properties/checks/type",keyword:"type",params:{type: "array"},message:"must be array"}];
return false;
}
}
var valid0 = _errs31 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.mark !== undefined){
let data19 = data.mark;
const _errs34 = errors;
const _errs35 = errors;
let valid5 = false;
const _errs36 = errors;
if(!(validate105(data19, {instancePath:instancePath+"/mark",parentData:data,parentDataProperty:"mark",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate105.errors : vErrors.concat(validate105.errors);
errors = vErrors.length;
}
var _valid2 = _errs36 === errors;
valid5 = valid5 || _valid2;
const _errs37 = errors;
if(data19 !== null){
const err4 = {instancePath:instancePath+"/mark",schemaPath:"#/properties/mark/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err4];
}
else {
vErrors.push(err4);
}
errors++;
}
var _valid2 = _errs37 === errors;
valid5 = valid5 || _valid2;
if(!valid5){
const err5 = {instancePath:instancePath+"/mark",schemaPath:"#/properties/mark/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err5];
}
else {
vErrors.push(err5);
}
errors++;
validate78.errors = vErrors;
return false;
}
else {
errors = _errs35;
if(vErrors !== null){
if(_errs35){
vErrors.length = _errs35;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs34 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.marked_at !== undefined){
let data20 = data.marked_at;
const _errs39 = errors;
const _errs40 = errors;
let valid6 = false;
const _errs41 = errors;
if(!(validate54(data20, {instancePath:instancePath+"/marked_at",parentData:data,parentDataProperty:"marked_at",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate54.errors : vErrors.concat(validate54.errors);
errors = vErrors.length;
}
var _valid3 = _errs41 === errors;
valid6 = valid6 || _valid3;
const _errs42 = errors;
if(data20 !== null){
const err6 = {instancePath:instancePath+"/marked_at",schemaPath:"#/properties/marked_at/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err6];
}
else {
vErrors.push(err6);
}
errors++;
}
var _valid3 = _errs42 === errors;
valid6 = valid6 || _valid3;
if(!valid6){
const err7 = {instancePath:instancePath+"/marked_at",schemaPath:"#/properties/marked_at/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err7];
}
else {
vErrors.push(err7);
}
errors++;
validate78.errors = vErrors;
return false;
}
else {
errors = _errs40;
if(vErrors !== null){
if(_errs40){
vErrors.length = _errs40;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs39 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.shown_anyway !== undefined){
const _errs44 = errors;
if(typeof data.shown_anyway !== "boolean"){
validate78.errors = [{instancePath:instancePath+"/shown_anyway",schemaPath:"#/properties/shown_anyway/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs44 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
}
}
}
}
}
}
}
}
}
}
}
}
}
}
else {
validate78.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate78.errors = vErrors;
return errors === 0;
}
validate78.evaluated = {"props":{"id":true,"company":true,"title":true,"place":true,"role_type":true,"source":true,"published_at":true,"found_at":true,"url":true,"deadline":true,"fit":true,"is_new":true,"today":true,"summary":true,"reason":true,"gaps":true,"checks":true,"mark":true,"marked_at":true,"shown_anyway":true},"dynamicProps":false,"dynamicItems":false};

const schema43 = {"type":"object","required":["id","company","title","place","role_type","source","url","found_at","today","kind","reason"],"properties":{"id":{"$ref":"#/$defs/AdId"},"company":{"$ref":"#/$defs/Company"},"title":{"$ref":"#/$defs/AdTitle"},"place":{"type":"string","maxLength":120},"role_type":{"anyOf":[{"$ref":"#/$defs/RoleType"},{"type":"null"}]},"source":{"$ref":"#/$defs/SourceId"},"url":{"$ref":"#/$defs/Url"},"found_at":{"$ref":"#/$defs/Timestamp"},"today":{"type":"boolean"},"kind":{"$ref":"#/$defs/HiddenKind"},"reason":{"type":"string","minLength":1,"maxLength":120}}};
const schema44 = {"type":"string","enum":["senior","years","masters","role","place","other"]};

function validate118(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate118.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(typeof data !== "string"){
validate118.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
if(!((((((data === "senior") || (data === "years")) || (data === "masters")) || (data === "role")) || (data === "place")) || (data === "other"))){
validate118.errors = [{instancePath,schemaPath:"#/enum",keyword:"enum",params:{allowedValues: schema44.enum},message:"must be equal to one of the allowed values"}];
return false;
}
validate118.errors = vErrors;
return errors === 0;
}
validate118.evaluated = {"dynamicProps":false,"dynamicItems":false};


function validate110(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate110.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((((((((((data.id === undefined) && (missing0 = "id")) || ((data.company === undefined) && (missing0 = "company"))) || ((data.title === undefined) && (missing0 = "title"))) || ((data.place === undefined) && (missing0 = "place"))) || ((data.role_type === undefined) && (missing0 = "role_type"))) || ((data.source === undefined) && (missing0 = "source"))) || ((data.url === undefined) && (missing0 = "url"))) || ((data.found_at === undefined) && (missing0 = "found_at"))) || ((data.today === undefined) && (missing0 = "today"))) || ((data.kind === undefined) && (missing0 = "kind"))) || ((data.reason === undefined) && (missing0 = "reason"))){
validate110.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.id !== undefined){
const _errs1 = errors;
if(!(validate79(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate79.errors : vErrors.concat(validate79.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.company !== undefined){
const _errs2 = errors;
if(!(validate81(data.company, {instancePath:instancePath+"/company",parentData:data,parentDataProperty:"company",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate81.errors : vErrors.concat(validate81.errors);
errors = vErrors.length;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.title !== undefined){
const _errs3 = errors;
if(!(validate83(data.title, {instancePath:instancePath+"/title",parentData:data,parentDataProperty:"title",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate83.errors : vErrors.concat(validate83.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.place !== undefined){
let data3 = data.place;
const _errs4 = errors;
if(errors === _errs4){
if(typeof data3 === "string"){
if(func1(data3) > 120){
validate110.errors = [{instancePath:instancePath+"/place",schemaPath:"#/properties/place/maxLength",keyword:"maxLength",params:{limit: 120},message:"must NOT have more than 120 characters"}];
return false;
}
}
else {
validate110.errors = [{instancePath:instancePath+"/place",schemaPath:"#/properties/place/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.role_type !== undefined){
let data4 = data.role_type;
const _errs6 = errors;
const _errs7 = errors;
let valid1 = false;
const _errs8 = errors;
if(!(validate85(data4, {instancePath:instancePath+"/role_type",parentData:data,parentDataProperty:"role_type",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate85.errors : vErrors.concat(validate85.errors);
errors = vErrors.length;
}
var _valid0 = _errs8 === errors;
valid1 = valid1 || _valid0;
const _errs9 = errors;
if(data4 !== null){
const err0 = {instancePath:instancePath+"/role_type",schemaPath:"#/properties/role_type/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
var _valid0 = _errs9 === errors;
valid1 = valid1 || _valid0;
if(!valid1){
const err1 = {instancePath:instancePath+"/role_type",schemaPath:"#/properties/role_type/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
validate110.errors = vErrors;
return false;
}
else {
errors = _errs7;
if(vErrors !== null){
if(_errs7){
vErrors.length = _errs7;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs6 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.source !== undefined){
const _errs11 = errors;
if(!(validate89(data.source, {instancePath:instancePath+"/source",parentData:data,parentDataProperty:"source",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate89.errors : vErrors.concat(validate89.errors);
errors = vErrors.length;
}
var valid0 = _errs11 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.url !== undefined){
const _errs12 = errors;
if(!(validate93(data.url, {instancePath:instancePath+"/url",parentData:data,parentDataProperty:"url",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate93.errors : vErrors.concat(validate93.errors);
errors = vErrors.length;
}
var valid0 = _errs12 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.found_at !== undefined){
const _errs13 = errors;
if(!(validate54(data.found_at, {instancePath:instancePath+"/found_at",parentData:data,parentDataProperty:"found_at",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate54.errors : vErrors.concat(validate54.errors);
errors = vErrors.length;
}
var valid0 = _errs13 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.today !== undefined){
const _errs14 = errors;
if(typeof data.today !== "boolean"){
validate110.errors = [{instancePath:instancePath+"/today",schemaPath:"#/properties/today/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs14 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.kind !== undefined){
const _errs16 = errors;
if(!(validate118(data.kind, {instancePath:instancePath+"/kind",parentData:data,parentDataProperty:"kind",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate118.errors : vErrors.concat(validate118.errors);
errors = vErrors.length;
}
var valid0 = _errs16 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.reason !== undefined){
let data10 = data.reason;
const _errs17 = errors;
if(errors === _errs17){
if(typeof data10 === "string"){
if(func1(data10) > 120){
validate110.errors = [{instancePath:instancePath+"/reason",schemaPath:"#/properties/reason/maxLength",keyword:"maxLength",params:{limit: 120},message:"must NOT have more than 120 characters"}];
return false;
}
else {
if(func1(data10) < 1){
validate110.errors = [{instancePath:instancePath+"/reason",schemaPath:"#/properties/reason/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"}];
return false;
}
}
}
else {
validate110.errors = [{instancePath:instancePath+"/reason",schemaPath:"#/properties/reason/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs17 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
}
}
}
}
}
else {
validate110.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate110.errors = vErrors;
return errors === 0;
}
validate110.evaluated = {"props":{"id":true,"company":true,"title":true,"place":true,"role_type":true,"source":true,"url":true,"found_at":true,"today":true,"kind":true,"reason":true},"dynamicProps":false,"dynamicItems":false};

const schema45 = {"type":"object","required":["senior","years","masters","role","place","other"],"properties":{"senior":{"$ref":"#/$defs/Count"},"years":{"$ref":"#/$defs/Count"},"masters":{"$ref":"#/$defs/Count"},"role":{"$ref":"#/$defs/Count"},"place":{"$ref":"#/$defs/Count"},"other":{"$ref":"#/$defs/Count"}}};

function validate122(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate122.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((((data.senior === undefined) && (missing0 = "senior")) || ((data.years === undefined) && (missing0 = "years"))) || ((data.masters === undefined) && (missing0 = "masters"))) || ((data.role === undefined) && (missing0 = "role"))) || ((data.place === undefined) && (missing0 = "place"))) || ((data.other === undefined) && (missing0 = "other"))){
validate122.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.senior !== undefined){
const _errs1 = errors;
if(!(validate60(data.senior, {instancePath:instancePath+"/senior",parentData:data,parentDataProperty:"senior",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.years !== undefined){
const _errs2 = errors;
if(!(validate60(data.years, {instancePath:instancePath+"/years",parentData:data,parentDataProperty:"years",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.masters !== undefined){
const _errs3 = errors;
if(!(validate60(data.masters, {instancePath:instancePath+"/masters",parentData:data,parentDataProperty:"masters",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.role !== undefined){
const _errs4 = errors;
if(!(validate60(data.role, {instancePath:instancePath+"/role",parentData:data,parentDataProperty:"role",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.place !== undefined){
const _errs5 = errors;
if(!(validate60(data.place, {instancePath:instancePath+"/place",parentData:data,parentDataProperty:"place",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.other !== undefined){
const _errs6 = errors;
if(!(validate60(data.other, {instancePath:instancePath+"/other",parentData:data,parentDataProperty:"other",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs6 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
else {
validate122.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate122.errors = vErrors;
return errors === 0;
}
validate122.evaluated = {"props":{"senior":true,"years":true,"masters":true,"role":true,"place":true,"other":true},"dynamicProps":false,"dynamicItems":false};

const schema46 = {"type":"object","required":["id","closed_on","company","title","source","reason","mark"],"properties":{"id":{"$ref":"#/$defs/AdId"},"closed_on":{"$ref":"#/$defs/Day"},"company":{"$ref":"#/$defs/Company"},"title":{"$ref":"#/$defs/AdTitle"},"source":{"$ref":"#/$defs/SourceId"},"reason":{"$ref":"#/$defs/ClosedReason"},"mark":{"$ref":"#/$defs/ClosedMark"}}};
const schema47 = {"type":"string","enum":["deadline","removed","filled"]};

function validate136(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate136.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(typeof data !== "string"){
validate136.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
if(!(((data === "deadline") || (data === "removed")) || (data === "filled"))){
validate136.errors = [{instancePath,schemaPath:"#/enum",keyword:"enum",params:{allowedValues: schema47.enum},message:"must be equal to one of the allowed values"}];
return false;
}
validate136.errors = vErrors;
return errors === 0;
}
validate136.evaluated = {"dynamicProps":false,"dynamicItems":false};

const schema48 = {"type":"string","enum":["applied","skipped","not_for_me","none"]};

function validate138(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate138.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(typeof data !== "string"){
validate138.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
if(!((((data === "applied") || (data === "skipped")) || (data === "not_for_me")) || (data === "none"))){
validate138.errors = [{instancePath,schemaPath:"#/enum",keyword:"enum",params:{allowedValues: schema48.enum},message:"must be equal to one of the allowed values"}];
return false;
}
validate138.errors = vErrors;
return errors === 0;
}
validate138.evaluated = {"dynamicProps":false,"dynamicItems":false};


function validate130(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate130.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((((((data.id === undefined) && (missing0 = "id")) || ((data.closed_on === undefined) && (missing0 = "closed_on"))) || ((data.company === undefined) && (missing0 = "company"))) || ((data.title === undefined) && (missing0 = "title"))) || ((data.source === undefined) && (missing0 = "source"))) || ((data.reason === undefined) && (missing0 = "reason"))) || ((data.mark === undefined) && (missing0 = "mark"))){
validate130.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.id !== undefined){
const _errs1 = errors;
if(!(validate79(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate79.errors : vErrors.concat(validate79.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.closed_on !== undefined){
const _errs2 = errors;
if(!(validate76(data.closed_on, {instancePath:instancePath+"/closed_on",parentData:data,parentDataProperty:"closed_on",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate76.errors : vErrors.concat(validate76.errors);
errors = vErrors.length;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.company !== undefined){
const _errs3 = errors;
if(!(validate81(data.company, {instancePath:instancePath+"/company",parentData:data,parentDataProperty:"company",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate81.errors : vErrors.concat(validate81.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.title !== undefined){
const _errs4 = errors;
if(!(validate83(data.title, {instancePath:instancePath+"/title",parentData:data,parentDataProperty:"title",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate83.errors : vErrors.concat(validate83.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.source !== undefined){
const _errs5 = errors;
if(!(validate89(data.source, {instancePath:instancePath+"/source",parentData:data,parentDataProperty:"source",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate89.errors : vErrors.concat(validate89.errors);
errors = vErrors.length;
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.reason !== undefined){
const _errs6 = errors;
if(!(validate136(data.reason, {instancePath:instancePath+"/reason",parentData:data,parentDataProperty:"reason",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate136.errors : vErrors.concat(validate136.errors);
errors = vErrors.length;
}
var valid0 = _errs6 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.mark !== undefined){
const _errs7 = errors;
if(!(validate138(data.mark, {instancePath:instancePath+"/mark",parentData:data,parentDataProperty:"mark",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate138.errors : vErrors.concat(validate138.errors);
errors = vErrors.length;
}
var valid0 = _errs7 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
}
else {
validate130.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate130.errors = vErrors;
return errors === 0;
}
validate130.evaluated = {"props":{"id":true,"closed_on":true,"company":true,"title":true,"source":true,"reason":true,"mark":true},"dynamicProps":false,"dynamicItems":false};


function validate74(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate74.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((((((((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.generated_at === undefined) && (missing0 = "generated_at"))) || ((data.as_of === undefined) && (missing0 = "as_of"))) || ((data.active === undefined) && (missing0 = "active"))) || ((data.active_total === undefined) && (missing0 = "active_total"))) || ((data.hidden === undefined) && (missing0 = "hidden"))) || ((data.hidden_total === undefined) && (missing0 = "hidden_total"))) || ((data.hidden_counts === undefined) && (missing0 = "hidden_counts"))) || ((data.closed === undefined) && (missing0 = "closed"))) || ((data.closed_total === undefined) && (missing0 = "closed_total"))){
validate74.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("ads.snapshot" !== data.type){
validate74.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "ads.snapshot"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate74.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.generated_at !== undefined){
const _errs3 = errors;
if(!(validate54(data.generated_at, {instancePath:instancePath+"/generated_at",parentData:data,parentDataProperty:"generated_at",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate54.errors : vErrors.concat(validate54.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.as_of !== undefined){
const _errs4 = errors;
if(!(validate76(data.as_of, {instancePath:instancePath+"/as_of",parentData:data,parentDataProperty:"as_of",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate76.errors : vErrors.concat(validate76.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.active !== undefined){
let data4 = data.active;
const _errs5 = errors;
if(errors === _errs5){
if(Array.isArray(data4)){
if(data4.length > 73){
validate74.errors = [{instancePath:instancePath+"/active",schemaPath:"#/properties/active/maxItems",keyword:"maxItems",params:{limit: 73},message:"must NOT have more than 73 items"}];
return false;
}
else {
var valid1 = true;
const len0 = data4.length;
for(let i0=0; i0<len0; i0++){
const _errs7 = errors;
if(!(validate78(data4[i0], {instancePath:instancePath+"/active/" + i0,parentData:data4,parentDataProperty:i0,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate78.errors : vErrors.concat(validate78.errors);
errors = vErrors.length;
}
var valid1 = _errs7 === errors;
if(!valid1){
break;
}
}
}
}
else {
validate74.errors = [{instancePath:instancePath+"/active",schemaPath:"#/properties/active/type",keyword:"type",params:{type: "array"},message:"must be array"}];
return false;
}
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.active_total !== undefined){
const _errs8 = errors;
if(!(validate60(data.active_total, {instancePath:instancePath+"/active_total",parentData:data,parentDataProperty:"active_total",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs8 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.hidden !== undefined){
let data7 = data.hidden;
const _errs9 = errors;
if(errors === _errs9){
if(Array.isArray(data7)){
if(data7.length > 500){
validate74.errors = [{instancePath:instancePath+"/hidden",schemaPath:"#/properties/hidden/maxItems",keyword:"maxItems",params:{limit: 500},message:"must NOT have more than 500 items"}];
return false;
}
else {
var valid2 = true;
const len1 = data7.length;
for(let i1=0; i1<len1; i1++){
const _errs11 = errors;
if(!(validate110(data7[i1], {instancePath:instancePath+"/hidden/" + i1,parentData:data7,parentDataProperty:i1,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate110.errors : vErrors.concat(validate110.errors);
errors = vErrors.length;
}
var valid2 = _errs11 === errors;
if(!valid2){
break;
}
}
}
}
else {
validate74.errors = [{instancePath:instancePath+"/hidden",schemaPath:"#/properties/hidden/type",keyword:"type",params:{type: "array"},message:"must be array"}];
return false;
}
}
var valid0 = _errs9 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.hidden_total !== undefined){
const _errs12 = errors;
if(!(validate60(data.hidden_total, {instancePath:instancePath+"/hidden_total",parentData:data,parentDataProperty:"hidden_total",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs12 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.hidden_counts !== undefined){
const _errs13 = errors;
if(!(validate122(data.hidden_counts, {instancePath:instancePath+"/hidden_counts",parentData:data,parentDataProperty:"hidden_counts",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate122.errors : vErrors.concat(validate122.errors);
errors = vErrors.length;
}
var valid0 = _errs13 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.closed !== undefined){
let data11 = data.closed;
const _errs14 = errors;
if(errors === _errs14){
if(Array.isArray(data11)){
if(data11.length > 200){
validate74.errors = [{instancePath:instancePath+"/closed",schemaPath:"#/properties/closed/maxItems",keyword:"maxItems",params:{limit: 200},message:"must NOT have more than 200 items"}];
return false;
}
else {
var valid3 = true;
const len2 = data11.length;
for(let i2=0; i2<len2; i2++){
const _errs16 = errors;
if(!(validate130(data11[i2], {instancePath:instancePath+"/closed/" + i2,parentData:data11,parentDataProperty:i2,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate130.errors : vErrors.concat(validate130.errors);
errors = vErrors.length;
}
var valid3 = _errs16 === errors;
if(!valid3){
break;
}
}
}
}
else {
validate74.errors = [{instancePath:instancePath+"/closed",schemaPath:"#/properties/closed/type",keyword:"type",params:{type: "array"},message:"must be array"}];
return false;
}
}
var valid0 = _errs14 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.closed_total !== undefined){
const _errs17 = errors;
if(!(validate60(data.closed_total, {instancePath:instancePath+"/closed_total",parentData:data,parentDataProperty:"closed_total",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs17 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
}
}
}
}
}
else {
validate74.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate74.errors = vErrors;
return errors === 0;
}
validate74.evaluated = {"props":{"type":true,"v":true,"generated_at":true,"as_of":true,"active":true,"active_total":true,"hidden":true,"hidden_total":true,"hidden_counts":true,"closed":true,"closed_total":true},"dynamicProps":false,"dynamicItems":false};

const schema49 = {"type":"object","required":["type","v","generated_at","applications","applications_total"],"properties":{"type":{"const":"applications.snapshot"},"v":{"const":1},"generated_at":{"$ref":"#/$defs/Timestamp"},"applications":{"type":"array","items":{"$ref":"#/$defs/Application"},"maxItems":145},"applications_total":{"$ref":"#/$defs/Count"}}};
const schema50 = {"type":"object","required":["id","ad_id","company","title","place","role_type","source","url","deadline","fit","status","note","applied_at","found_at","cv_name","checks","history","removed"],"properties":{"id":{"$ref":"#/$defs/ApplicationId"},"ad_id":{"anyOf":[{"$ref":"#/$defs/AdId"},{"type":"null"}]},"company":{"$ref":"#/$defs/Company"},"title":{"type":"string","maxLength":200},"place":{"type":"string","maxLength":120},"role_type":{"anyOf":[{"$ref":"#/$defs/RoleType"},{"type":"null"}]},"source":{"anyOf":[{"$ref":"#/$defs/SourceId"},{"type":"null"}]},"url":{"anyOf":[{"$ref":"#/$defs/Url"},{"type":"null"}]},"deadline":{"anyOf":[{"$ref":"#/$defs/Day"},{"type":"null"}]},"fit":{"anyOf":[{"$ref":"#/$defs/Fit"},{"type":"null"}]},"status":{"$ref":"#/$defs/ApplicationStatus"},"note":{"$ref":"#/$defs/Note"},"applied_at":{"$ref":"#/$defs/Timestamp"},"found_at":{"anyOf":[{"$ref":"#/$defs/Timestamp"},{"type":"null"}]},"cv_name":{"anyOf":[{"type":"string","maxLength":120},{"type":"null"}]},"checks":{"type":"array","items":{"$ref":"#/$defs/Check"},"maxItems":5},"history":{"type":"array","items":{"$ref":"#/$defs/HistoryEntry"},"maxItems":20},"removed":{"type":"boolean"}}};
const schema51 = {"type":"integer","minimum":1,"maximum":9007199254740991};

function validate145(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate145.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(!(((typeof data == "number") && (!(data % 1) && !isNaN(data))) && (isFinite(data)))){
validate145.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "integer"},message:"must be integer"}];
return false;
}
if(errors === 0){
if((typeof data == "number") && (isFinite(data))){
if(data > 9007199254740991 || isNaN(data)){
validate145.errors = [{instancePath,schemaPath:"#/maximum",keyword:"maximum",params:{comparison: "<=", limit: 9007199254740991},message:"must be <= 9007199254740991"}];
return false;
}
else {
if(data < 1 || isNaN(data)){
validate145.errors = [{instancePath,schemaPath:"#/minimum",keyword:"minimum",params:{comparison: ">=", limit: 1},message:"must be >= 1"}];
return false;
}
}
}
}
validate145.errors = vErrors;
return errors === 0;
}
validate145.evaluated = {"dynamicProps":false,"dynamicItems":false};

const schema52 = {"type":"string","enum":["waiting","interview","offer","accepted","rejected"]};

function validate154(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate154.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(typeof data !== "string"){
validate154.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
if(!(((((data === "waiting") || (data === "interview")) || (data === "offer")) || (data === "accepted")) || (data === "rejected"))){
validate154.errors = [{instancePath,schemaPath:"#/enum",keyword:"enum",params:{allowedValues: schema52.enum},message:"must be equal to one of the allowed values"}];
return false;
}
validate154.errors = vErrors;
return errors === 0;
}
validate154.evaluated = {"dynamicProps":false,"dynamicItems":false};

const schema53 = {"type":"string","maxLength":2000};

function validate156(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate156.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(typeof data === "string"){
if(func1(data) > 2000){
validate156.errors = [{instancePath,schemaPath:"#/maxLength",keyword:"maxLength",params:{limit: 2000},message:"must NOT have more than 2000 characters"}];
return false;
}
}
else {
validate156.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
validate156.errors = vErrors;
return errors === 0;
}
validate156.evaluated = {"dynamicProps":false,"dynamicItems":false};

const schema54 = {"type":"object","required":["at","text"],"properties":{"at":{"$ref":"#/$defs/Timestamp"},"text":{"type":"string","minLength":1,"maxLength":200}}};

function validate161(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate161.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((data.at === undefined) && (missing0 = "at")) || ((data.text === undefined) && (missing0 = "text"))){
validate161.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.at !== undefined){
const _errs1 = errors;
if(!(validate54(data.at, {instancePath:instancePath+"/at",parentData:data,parentDataProperty:"at",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate54.errors : vErrors.concat(validate54.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.text !== undefined){
let data1 = data.text;
const _errs2 = errors;
if(errors === _errs2){
if(typeof data1 === "string"){
if(func1(data1) > 200){
validate161.errors = [{instancePath:instancePath+"/text",schemaPath:"#/properties/text/maxLength",keyword:"maxLength",params:{limit: 200},message:"must NOT have more than 200 characters"}];
return false;
}
else {
if(func1(data1) < 1){
validate161.errors = [{instancePath:instancePath+"/text",schemaPath:"#/properties/text/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"}];
return false;
}
}
}
else {
validate161.errors = [{instancePath:instancePath+"/text",schemaPath:"#/properties/text/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
}
}
}
else {
validate161.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate161.errors = vErrors;
return errors === 0;
}
validate161.evaluated = {"props":{"at":true,"text":true},"dynamicProps":false,"dynamicItems":false};


function validate144(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate144.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((((((((((((((((data.id === undefined) && (missing0 = "id")) || ((data.ad_id === undefined) && (missing0 = "ad_id"))) || ((data.company === undefined) && (missing0 = "company"))) || ((data.title === undefined) && (missing0 = "title"))) || ((data.place === undefined) && (missing0 = "place"))) || ((data.role_type === undefined) && (missing0 = "role_type"))) || ((data.source === undefined) && (missing0 = "source"))) || ((data.url === undefined) && (missing0 = "url"))) || ((data.deadline === undefined) && (missing0 = "deadline"))) || ((data.fit === undefined) && (missing0 = "fit"))) || ((data.status === undefined) && (missing0 = "status"))) || ((data.note === undefined) && (missing0 = "note"))) || ((data.applied_at === undefined) && (missing0 = "applied_at"))) || ((data.found_at === undefined) && (missing0 = "found_at"))) || ((data.cv_name === undefined) && (missing0 = "cv_name"))) || ((data.checks === undefined) && (missing0 = "checks"))) || ((data.history === undefined) && (missing0 = "history"))) || ((data.removed === undefined) && (missing0 = "removed"))){
validate144.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.id !== undefined){
const _errs1 = errors;
if(!(validate145(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate145.errors : vErrors.concat(validate145.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.ad_id !== undefined){
let data1 = data.ad_id;
const _errs2 = errors;
const _errs3 = errors;
let valid1 = false;
const _errs4 = errors;
if(!(validate79(data1, {instancePath:instancePath+"/ad_id",parentData:data,parentDataProperty:"ad_id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate79.errors : vErrors.concat(validate79.errors);
errors = vErrors.length;
}
var _valid0 = _errs4 === errors;
valid1 = valid1 || _valid0;
const _errs5 = errors;
if(data1 !== null){
const err0 = {instancePath:instancePath+"/ad_id",schemaPath:"#/properties/ad_id/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
var _valid0 = _errs5 === errors;
valid1 = valid1 || _valid0;
if(!valid1){
const err1 = {instancePath:instancePath+"/ad_id",schemaPath:"#/properties/ad_id/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
validate144.errors = vErrors;
return false;
}
else {
errors = _errs3;
if(vErrors !== null){
if(_errs3){
vErrors.length = _errs3;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.company !== undefined){
const _errs7 = errors;
if(!(validate81(data.company, {instancePath:instancePath+"/company",parentData:data,parentDataProperty:"company",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate81.errors : vErrors.concat(validate81.errors);
errors = vErrors.length;
}
var valid0 = _errs7 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.title !== undefined){
let data3 = data.title;
const _errs8 = errors;
if(errors === _errs8){
if(typeof data3 === "string"){
if(func1(data3) > 200){
validate144.errors = [{instancePath:instancePath+"/title",schemaPath:"#/properties/title/maxLength",keyword:"maxLength",params:{limit: 200},message:"must NOT have more than 200 characters"}];
return false;
}
}
else {
validate144.errors = [{instancePath:instancePath+"/title",schemaPath:"#/properties/title/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs8 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.place !== undefined){
let data4 = data.place;
const _errs10 = errors;
if(errors === _errs10){
if(typeof data4 === "string"){
if(func1(data4) > 120){
validate144.errors = [{instancePath:instancePath+"/place",schemaPath:"#/properties/place/maxLength",keyword:"maxLength",params:{limit: 120},message:"must NOT have more than 120 characters"}];
return false;
}
}
else {
validate144.errors = [{instancePath:instancePath+"/place",schemaPath:"#/properties/place/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs10 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.role_type !== undefined){
let data5 = data.role_type;
const _errs12 = errors;
const _errs13 = errors;
let valid2 = false;
const _errs14 = errors;
if(!(validate85(data5, {instancePath:instancePath+"/role_type",parentData:data,parentDataProperty:"role_type",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate85.errors : vErrors.concat(validate85.errors);
errors = vErrors.length;
}
var _valid1 = _errs14 === errors;
valid2 = valid2 || _valid1;
const _errs15 = errors;
if(data5 !== null){
const err2 = {instancePath:instancePath+"/role_type",schemaPath:"#/properties/role_type/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err2];
}
else {
vErrors.push(err2);
}
errors++;
}
var _valid1 = _errs15 === errors;
valid2 = valid2 || _valid1;
if(!valid2){
const err3 = {instancePath:instancePath+"/role_type",schemaPath:"#/properties/role_type/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err3];
}
else {
vErrors.push(err3);
}
errors++;
validate144.errors = vErrors;
return false;
}
else {
errors = _errs13;
if(vErrors !== null){
if(_errs13){
vErrors.length = _errs13;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs12 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.source !== undefined){
let data6 = data.source;
const _errs17 = errors;
const _errs18 = errors;
let valid3 = false;
const _errs19 = errors;
if(!(validate89(data6, {instancePath:instancePath+"/source",parentData:data,parentDataProperty:"source",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate89.errors : vErrors.concat(validate89.errors);
errors = vErrors.length;
}
var _valid2 = _errs19 === errors;
valid3 = valid3 || _valid2;
const _errs20 = errors;
if(data6 !== null){
const err4 = {instancePath:instancePath+"/source",schemaPath:"#/properties/source/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err4];
}
else {
vErrors.push(err4);
}
errors++;
}
var _valid2 = _errs20 === errors;
valid3 = valid3 || _valid2;
if(!valid3){
const err5 = {instancePath:instancePath+"/source",schemaPath:"#/properties/source/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err5];
}
else {
vErrors.push(err5);
}
errors++;
validate144.errors = vErrors;
return false;
}
else {
errors = _errs18;
if(vErrors !== null){
if(_errs18){
vErrors.length = _errs18;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs17 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.url !== undefined){
let data7 = data.url;
const _errs22 = errors;
const _errs23 = errors;
let valid4 = false;
const _errs24 = errors;
if(!(validate93(data7, {instancePath:instancePath+"/url",parentData:data,parentDataProperty:"url",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate93.errors : vErrors.concat(validate93.errors);
errors = vErrors.length;
}
var _valid3 = _errs24 === errors;
valid4 = valid4 || _valid3;
const _errs25 = errors;
if(data7 !== null){
const err6 = {instancePath:instancePath+"/url",schemaPath:"#/properties/url/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err6];
}
else {
vErrors.push(err6);
}
errors++;
}
var _valid3 = _errs25 === errors;
valid4 = valid4 || _valid3;
if(!valid4){
const err7 = {instancePath:instancePath+"/url",schemaPath:"#/properties/url/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err7];
}
else {
vErrors.push(err7);
}
errors++;
validate144.errors = vErrors;
return false;
}
else {
errors = _errs23;
if(vErrors !== null){
if(_errs23){
vErrors.length = _errs23;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs22 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.deadline !== undefined){
let data8 = data.deadline;
const _errs27 = errors;
const _errs28 = errors;
let valid5 = false;
const _errs29 = errors;
if(!(validate76(data8, {instancePath:instancePath+"/deadline",parentData:data,parentDataProperty:"deadline",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate76.errors : vErrors.concat(validate76.errors);
errors = vErrors.length;
}
var _valid4 = _errs29 === errors;
valid5 = valid5 || _valid4;
const _errs30 = errors;
if(data8 !== null){
const err8 = {instancePath:instancePath+"/deadline",schemaPath:"#/properties/deadline/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err8];
}
else {
vErrors.push(err8);
}
errors++;
}
var _valid4 = _errs30 === errors;
valid5 = valid5 || _valid4;
if(!valid5){
const err9 = {instancePath:instancePath+"/deadline",schemaPath:"#/properties/deadline/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err9];
}
else {
vErrors.push(err9);
}
errors++;
validate144.errors = vErrors;
return false;
}
else {
errors = _errs28;
if(vErrors !== null){
if(_errs28){
vErrors.length = _errs28;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs27 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.fit !== undefined){
let data9 = data.fit;
const _errs32 = errors;
const _errs33 = errors;
let valid6 = false;
const _errs34 = errors;
if(!(validate96(data9, {instancePath:instancePath+"/fit",parentData:data,parentDataProperty:"fit",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate96.errors : vErrors.concat(validate96.errors);
errors = vErrors.length;
}
var _valid5 = _errs34 === errors;
valid6 = valid6 || _valid5;
const _errs35 = errors;
if(data9 !== null){
const err10 = {instancePath:instancePath+"/fit",schemaPath:"#/properties/fit/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err10];
}
else {
vErrors.push(err10);
}
errors++;
}
var _valid5 = _errs35 === errors;
valid6 = valid6 || _valid5;
if(!valid6){
const err11 = {instancePath:instancePath+"/fit",schemaPath:"#/properties/fit/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err11];
}
else {
vErrors.push(err11);
}
errors++;
validate144.errors = vErrors;
return false;
}
else {
errors = _errs33;
if(vErrors !== null){
if(_errs33){
vErrors.length = _errs33;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs32 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.status !== undefined){
const _errs37 = errors;
if(!(validate154(data.status, {instancePath:instancePath+"/status",parentData:data,parentDataProperty:"status",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate154.errors : vErrors.concat(validate154.errors);
errors = vErrors.length;
}
var valid0 = _errs37 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.note !== undefined){
const _errs38 = errors;
if(!(validate156(data.note, {instancePath:instancePath+"/note",parentData:data,parentDataProperty:"note",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate156.errors : vErrors.concat(validate156.errors);
errors = vErrors.length;
}
var valid0 = _errs38 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.applied_at !== undefined){
const _errs39 = errors;
if(!(validate54(data.applied_at, {instancePath:instancePath+"/applied_at",parentData:data,parentDataProperty:"applied_at",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate54.errors : vErrors.concat(validate54.errors);
errors = vErrors.length;
}
var valid0 = _errs39 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.found_at !== undefined){
let data13 = data.found_at;
const _errs40 = errors;
const _errs41 = errors;
let valid7 = false;
const _errs42 = errors;
if(!(validate54(data13, {instancePath:instancePath+"/found_at",parentData:data,parentDataProperty:"found_at",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate54.errors : vErrors.concat(validate54.errors);
errors = vErrors.length;
}
var _valid6 = _errs42 === errors;
valid7 = valid7 || _valid6;
const _errs43 = errors;
if(data13 !== null){
const err12 = {instancePath:instancePath+"/found_at",schemaPath:"#/properties/found_at/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err12];
}
else {
vErrors.push(err12);
}
errors++;
}
var _valid6 = _errs43 === errors;
valid7 = valid7 || _valid6;
if(!valid7){
const err13 = {instancePath:instancePath+"/found_at",schemaPath:"#/properties/found_at/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err13];
}
else {
vErrors.push(err13);
}
errors++;
validate144.errors = vErrors;
return false;
}
else {
errors = _errs41;
if(vErrors !== null){
if(_errs41){
vErrors.length = _errs41;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs40 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.cv_name !== undefined){
let data14 = data.cv_name;
const _errs45 = errors;
const _errs46 = errors;
let valid8 = false;
const _errs47 = errors;
if(errors === _errs47){
if(typeof data14 === "string"){
if(func1(data14) > 120){
const err14 = {instancePath:instancePath+"/cv_name",schemaPath:"#/properties/cv_name/anyOf/0/maxLength",keyword:"maxLength",params:{limit: 120},message:"must NOT have more than 120 characters"};
if(vErrors === null){
vErrors = [err14];
}
else {
vErrors.push(err14);
}
errors++;
}
}
else {
const err15 = {instancePath:instancePath+"/cv_name",schemaPath:"#/properties/cv_name/anyOf/0/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err15];
}
else {
vErrors.push(err15);
}
errors++;
}
}
var _valid7 = _errs47 === errors;
valid8 = valid8 || _valid7;
const _errs49 = errors;
if(data14 !== null){
const err16 = {instancePath:instancePath+"/cv_name",schemaPath:"#/properties/cv_name/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err16];
}
else {
vErrors.push(err16);
}
errors++;
}
var _valid7 = _errs49 === errors;
valid8 = valid8 || _valid7;
if(!valid8){
const err17 = {instancePath:instancePath+"/cv_name",schemaPath:"#/properties/cv_name/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err17];
}
else {
vErrors.push(err17);
}
errors++;
validate144.errors = vErrors;
return false;
}
else {
errors = _errs46;
if(vErrors !== null){
if(_errs46){
vErrors.length = _errs46;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs45 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.checks !== undefined){
let data15 = data.checks;
const _errs51 = errors;
if(errors === _errs51){
if(Array.isArray(data15)){
if(data15.length > 5){
validate144.errors = [{instancePath:instancePath+"/checks",schemaPath:"#/properties/checks/maxItems",keyword:"maxItems",params:{limit: 5},message:"must NOT have more than 5 items"}];
return false;
}
else {
var valid9 = true;
const len0 = data15.length;
for(let i0=0; i0<len0; i0++){
const _errs53 = errors;
if(!(validate101(data15[i0], {instancePath:instancePath+"/checks/" + i0,parentData:data15,parentDataProperty:i0,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate101.errors : vErrors.concat(validate101.errors);
errors = vErrors.length;
}
var valid9 = _errs53 === errors;
if(!valid9){
break;
}
}
}
}
else {
validate144.errors = [{instancePath:instancePath+"/checks",schemaPath:"#/properties/checks/type",keyword:"type",params:{type: "array"},message:"must be array"}];
return false;
}
}
var valid0 = _errs51 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.history !== undefined){
let data17 = data.history;
const _errs54 = errors;
if(errors === _errs54){
if(Array.isArray(data17)){
if(data17.length > 20){
validate144.errors = [{instancePath:instancePath+"/history",schemaPath:"#/properties/history/maxItems",keyword:"maxItems",params:{limit: 20},message:"must NOT have more than 20 items"}];
return false;
}
else {
var valid10 = true;
const len1 = data17.length;
for(let i1=0; i1<len1; i1++){
const _errs56 = errors;
if(!(validate161(data17[i1], {instancePath:instancePath+"/history/" + i1,parentData:data17,parentDataProperty:i1,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate161.errors : vErrors.concat(validate161.errors);
errors = vErrors.length;
}
var valid10 = _errs56 === errors;
if(!valid10){
break;
}
}
}
}
else {
validate144.errors = [{instancePath:instancePath+"/history",schemaPath:"#/properties/history/type",keyword:"type",params:{type: "array"},message:"must be array"}];
return false;
}
}
var valid0 = _errs54 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.removed !== undefined){
const _errs57 = errors;
if(typeof data.removed !== "boolean"){
validate144.errors = [{instancePath:instancePath+"/removed",schemaPath:"#/properties/removed/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs57 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
}
}
}
}
}
}
}
}
}
}
}
}
else {
validate144.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate144.errors = vErrors;
return errors === 0;
}
validate144.evaluated = {"props":{"id":true,"ad_id":true,"company":true,"title":true,"place":true,"role_type":true,"source":true,"url":true,"deadline":true,"fit":true,"status":true,"note":true,"applied_at":true,"found_at":true,"cv_name":true,"checks":true,"history":true,"removed":true},"dynamicProps":false,"dynamicItems":false};


function validate142(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate142.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.generated_at === undefined) && (missing0 = "generated_at"))) || ((data.applications === undefined) && (missing0 = "applications"))) || ((data.applications_total === undefined) && (missing0 = "applications_total"))){
validate142.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("applications.snapshot" !== data.type){
validate142.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "applications.snapshot"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate142.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.generated_at !== undefined){
const _errs3 = errors;
if(!(validate54(data.generated_at, {instancePath:instancePath+"/generated_at",parentData:data,parentDataProperty:"generated_at",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate54.errors : vErrors.concat(validate54.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.applications !== undefined){
let data3 = data.applications;
const _errs4 = errors;
if(errors === _errs4){
if(Array.isArray(data3)){
if(data3.length > 145){
validate142.errors = [{instancePath:instancePath+"/applications",schemaPath:"#/properties/applications/maxItems",keyword:"maxItems",params:{limit: 145},message:"must NOT have more than 145 items"}];
return false;
}
else {
var valid1 = true;
const len0 = data3.length;
for(let i0=0; i0<len0; i0++){
const _errs6 = errors;
if(!(validate144(data3[i0], {instancePath:instancePath+"/applications/" + i0,parentData:data3,parentDataProperty:i0,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate144.errors : vErrors.concat(validate144.errors);
errors = vErrors.length;
}
var valid1 = _errs6 === errors;
if(!valid1){
break;
}
}
}
}
else {
validate142.errors = [{instancePath:instancePath+"/applications",schemaPath:"#/properties/applications/type",keyword:"type",params:{type: "array"},message:"must be array"}];
return false;
}
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.applications_total !== undefined){
const _errs7 = errors;
if(!(validate60(data.applications_total, {instancePath:instancePath+"/applications_total",parentData:data,parentDataProperty:"applications_total",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs7 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
else {
validate142.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate142.errors = vErrors;
return errors === 0;
}
validate142.evaluated = {"props":{"type":true,"v":true,"generated_at":true,"applications":true,"applications_total":true},"dynamicProps":false,"dynamicItems":false};

const schema55 = {"type":"object","required":["type","v","roles","targets","sources","schedule","notify","model","language"],"properties":{"type":{"const":"config.snapshot"},"v":{"const":1},"roles":{"type":"array","items":{"$ref":"#/$defs/RoleOption"},"maxItems":9},"targets":{"$ref":"#/$defs/Targets"},"sources":{"$ref":"#/$defs/Sources"},"schedule":{"$ref":"#/$defs/Schedule"},"notify":{"$ref":"#/$defs/Notify"},"model":{"$ref":"#/$defs/ModelConfig"},"language":{"$ref":"#/$defs/Language"}}};
const schema56 = {"type":"object","required":["id","label"],"properties":{"id":{"$ref":"#/$defs/RoleId"},"label":{"type":"string","minLength":1,"maxLength":40}}};

function validate167(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate167.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((data.id === undefined) && (missing0 = "id")) || ((data.label === undefined) && (missing0 = "label"))){
validate167.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.id !== undefined){
const _errs1 = errors;
if(!(validate86(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate86.errors : vErrors.concat(validate86.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.label !== undefined){
let data1 = data.label;
const _errs2 = errors;
if(errors === _errs2){
if(typeof data1 === "string"){
if(func1(data1) > 40){
validate167.errors = [{instancePath:instancePath+"/label",schemaPath:"#/properties/label/maxLength",keyword:"maxLength",params:{limit: 40},message:"must NOT have more than 40 characters"}];
return false;
}
else {
if(func1(data1) < 1){
validate167.errors = [{instancePath:instancePath+"/label",schemaPath:"#/properties/label/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"}];
return false;
}
}
}
else {
validate167.errors = [{instancePath:instancePath+"/label",schemaPath:"#/properties/label/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
}
}
}
else {
validate167.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate167.errors = vErrors;
return errors === 0;
}
validate167.evaluated = {"props":{"id":true,"label":true},"dynamicProps":false,"dynamicItems":false};

const schema57 = {"type":"object","required":["subfields","levels","years","hide_masters","places","relocate"],"properties":{"subfields":{"$ref":"#/$defs/RoleIds"},"levels":{"$ref":"#/$defs/Levels"},"years":{"$ref":"#/$defs/YearBands"},"hide_masters":{"type":"boolean"},"places":{"$ref":"#/$defs/Places"},"relocate":{"type":"boolean"}}};
const schema58 = {"type":"array","items":{"$ref":"#/$defs/RoleId"},"uniqueItems":true,"maxItems":9};
const func0 = resolveExport(func0Module, "default");

function validate171(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate171.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(Array.isArray(data)){
if(data.length > 9){
validate171.errors = [{instancePath,schemaPath:"#/maxItems",keyword:"maxItems",params:{limit: 9},message:"must NOT have more than 9 items"}];
return false;
}
else {
var valid0 = true;
const len0 = data.length;
for(let i0=0; i0<len0; i0++){
const _errs1 = errors;
if(!(validate86(data[i0], {instancePath:instancePath+"/" + i0,parentData:data,parentDataProperty:i0,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate86.errors : vErrors.concat(validate86.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
if(!valid0){
break;
}
}
if(valid0){
let i1 = data.length;
let j0;
if(i1 > 1){
outer0:
for(;i1--;){
for(j0 = i1; j0--;){
if(func0(data[i1], data[j0])){
validate171.errors = [{instancePath,schemaPath:"#/uniqueItems",keyword:"uniqueItems",params:{i: i1, j: j0},message:"must NOT have duplicate items (items ## "+j0+" and "+i1+" are identical)"}];
return false;
break outer0;
}
}
}
}
}
}
}
else {
validate171.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "array"},message:"must be array"}];
return false;
}
}
validate171.errors = vErrors;
return errors === 0;
}
validate171.evaluated = {"items":true,"dynamicProps":false,"dynamicItems":false};

const schema59 = {"type":"array","items":{"$ref":"#/$defs/Level"},"uniqueItems":true,"maxItems":5};
const schema60 = {"type":"string","enum":["junior","mid","senior","lead","none"]};

function validate175(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate175.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(typeof data !== "string"){
validate175.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
if(!(((((data === "junior") || (data === "mid")) || (data === "senior")) || (data === "lead")) || (data === "none"))){
validate175.errors = [{instancePath,schemaPath:"#/enum",keyword:"enum",params:{allowedValues: schema60.enum},message:"must be equal to one of the allowed values"}];
return false;
}
validate175.errors = vErrors;
return errors === 0;
}
validate175.evaluated = {"dynamicProps":false,"dynamicItems":false};


function validate174(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate174.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(Array.isArray(data)){
if(data.length > 5){
validate174.errors = [{instancePath,schemaPath:"#/maxItems",keyword:"maxItems",params:{limit: 5},message:"must NOT have more than 5 items"}];
return false;
}
else {
var valid0 = true;
const len0 = data.length;
for(let i0=0; i0<len0; i0++){
const _errs1 = errors;
if(!(validate175(data[i0], {instancePath:instancePath+"/" + i0,parentData:data,parentDataProperty:i0,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate175.errors : vErrors.concat(validate175.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
if(!valid0){
break;
}
}
if(valid0){
let i1 = data.length;
let j0;
if(i1 > 1){
outer0:
for(;i1--;){
for(j0 = i1; j0--;){
if(func0(data[i1], data[j0])){
validate174.errors = [{instancePath,schemaPath:"#/uniqueItems",keyword:"uniqueItems",params:{i: i1, j: j0},message:"must NOT have duplicate items (items ## "+j0+" and "+i1+" are identical)"}];
return false;
break outer0;
}
}
}
}
}
}
}
else {
validate174.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "array"},message:"must be array"}];
return false;
}
}
validate174.errors = vErrors;
return errors === 0;
}
validate174.evaluated = {"items":true,"dynamicProps":false,"dynamicItems":false};

const schema61 = {"type":"array","items":{"$ref":"#/$defs/YearBand"},"uniqueItems":true,"maxItems":4};
const schema62 = {"type":"string","enum":["0-1","1-3","3-6","6+"]};

function validate179(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate179.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(typeof data !== "string"){
validate179.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
if(!((((data === "0-1") || (data === "1-3")) || (data === "3-6")) || (data === "6+"))){
validate179.errors = [{instancePath,schemaPath:"#/enum",keyword:"enum",params:{allowedValues: schema62.enum},message:"must be equal to one of the allowed values"}];
return false;
}
validate179.errors = vErrors;
return errors === 0;
}
validate179.evaluated = {"dynamicProps":false,"dynamicItems":false};


function validate178(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate178.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(Array.isArray(data)){
if(data.length > 4){
validate178.errors = [{instancePath,schemaPath:"#/maxItems",keyword:"maxItems",params:{limit: 4},message:"must NOT have more than 4 items"}];
return false;
}
else {
var valid0 = true;
const len0 = data.length;
for(let i0=0; i0<len0; i0++){
const _errs1 = errors;
if(!(validate179(data[i0], {instancePath:instancePath+"/" + i0,parentData:data,parentDataProperty:i0,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate179.errors : vErrors.concat(validate179.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
if(!valid0){
break;
}
}
if(valid0){
let i1 = data.length;
let j0;
if(i1 > 1){
outer0:
for(;i1--;){
for(j0 = i1; j0--;){
if(func0(data[i1], data[j0])){
validate178.errors = [{instancePath,schemaPath:"#/uniqueItems",keyword:"uniqueItems",params:{i: i1, j: j0},message:"must NOT have duplicate items (items ## "+j0+" and "+i1+" are identical)"}];
return false;
break outer0;
}
}
}
}
}
}
}
else {
validate178.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "array"},message:"must be array"}];
return false;
}
}
validate178.errors = vErrors;
return errors === 0;
}
validate178.evaluated = {"items":true,"dynamicProps":false,"dynamicItems":false};

const schema63 = {"type":"array","items":{"type":"string","minLength":1,"maxLength":80},"uniqueItems":true,"maxItems":20};

function validate182(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate182.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(Array.isArray(data)){
if(data.length > 20){
validate182.errors = [{instancePath,schemaPath:"#/maxItems",keyword:"maxItems",params:{limit: 20},message:"must NOT have more than 20 items"}];
return false;
}
else {
var valid0 = true;
const len0 = data.length;
for(let i0=0; i0<len0; i0++){
let data0 = data[i0];
const _errs1 = errors;
if(errors === _errs1){
if(typeof data0 === "string"){
if(func1(data0) > 80){
validate182.errors = [{instancePath:instancePath+"/" + i0,schemaPath:"#/items/maxLength",keyword:"maxLength",params:{limit: 80},message:"must NOT have more than 80 characters"}];
return false;
}
else {
if(func1(data0) < 1){
validate182.errors = [{instancePath:instancePath+"/" + i0,schemaPath:"#/items/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"}];
return false;
}
}
}
else {
validate182.errors = [{instancePath:instancePath+"/" + i0,schemaPath:"#/items/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs1 === errors;
if(!valid0){
break;
}
}
if(valid0){
let i1 = data.length;
let j0;
if(i1 > 1){
const indices0 = {};
for(;i1--;){
let item0 = data[i1];
if(typeof item0 !== "string"){
continue;
}
if(typeof indices0[item0] == "number"){
j0 = indices0[item0];
validate182.errors = [{instancePath,schemaPath:"#/uniqueItems",keyword:"uniqueItems",params:{i: i1, j: j0},message:"must NOT have duplicate items (items ## "+j0+" and "+i1+" are identical)"}];
return false;
break;
}
indices0[item0] = i1;
}
}
}
}
}
else {
validate182.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "array"},message:"must be array"}];
return false;
}
}
validate182.errors = vErrors;
return errors === 0;
}
validate182.evaluated = {"items":true,"dynamicProps":false,"dynamicItems":false};


function validate170(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate170.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((((data.subfields === undefined) && (missing0 = "subfields")) || ((data.levels === undefined) && (missing0 = "levels"))) || ((data.years === undefined) && (missing0 = "years"))) || ((data.hide_masters === undefined) && (missing0 = "hide_masters"))) || ((data.places === undefined) && (missing0 = "places"))) || ((data.relocate === undefined) && (missing0 = "relocate"))){
validate170.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.subfields !== undefined){
const _errs1 = errors;
if(!(validate171(data.subfields, {instancePath:instancePath+"/subfields",parentData:data,parentDataProperty:"subfields",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate171.errors : vErrors.concat(validate171.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.levels !== undefined){
const _errs2 = errors;
if(!(validate174(data.levels, {instancePath:instancePath+"/levels",parentData:data,parentDataProperty:"levels",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate174.errors : vErrors.concat(validate174.errors);
errors = vErrors.length;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.years !== undefined){
const _errs3 = errors;
if(!(validate178(data.years, {instancePath:instancePath+"/years",parentData:data,parentDataProperty:"years",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate178.errors : vErrors.concat(validate178.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.hide_masters !== undefined){
const _errs4 = errors;
if(typeof data.hide_masters !== "boolean"){
validate170.errors = [{instancePath:instancePath+"/hide_masters",schemaPath:"#/properties/hide_masters/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.places !== undefined){
const _errs6 = errors;
if(!(validate182(data.places, {instancePath:instancePath+"/places",parentData:data,parentDataProperty:"places",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate182.errors : vErrors.concat(validate182.errors);
errors = vErrors.length;
}
var valid0 = _errs6 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.relocate !== undefined){
const _errs7 = errors;
if(typeof data.relocate !== "boolean"){
validate170.errors = [{instancePath:instancePath+"/relocate",schemaPath:"#/properties/relocate/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs7 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
else {
validate170.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate170.errors = vErrors;
return errors === 0;
}
validate170.evaluated = {"props":{"subfields":true,"levels":true,"years":true,"hide_masters":true,"places":true,"relocate":true},"dynamicProps":false,"dynamicItems":false};

const schema64 = {"type":"object","required":["nav","employers","finn","linkedin"],"properties":{"nav":{"type":"boolean"},"employers":{"type":"boolean"},"finn":{"type":"boolean"},"linkedin":{"type":"boolean"}}};

function validate185(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate185.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((data.nav === undefined) && (missing0 = "nav")) || ((data.employers === undefined) && (missing0 = "employers"))) || ((data.finn === undefined) && (missing0 = "finn"))) || ((data.linkedin === undefined) && (missing0 = "linkedin"))){
validate185.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.nav !== undefined){
const _errs1 = errors;
if(typeof data.nav !== "boolean"){
validate185.errors = [{instancePath:instancePath+"/nav",schemaPath:"#/properties/nav/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.employers !== undefined){
const _errs3 = errors;
if(typeof data.employers !== "boolean"){
validate185.errors = [{instancePath:instancePath+"/employers",schemaPath:"#/properties/employers/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.finn !== undefined){
const _errs5 = errors;
if(typeof data.finn !== "boolean"){
validate185.errors = [{instancePath:instancePath+"/finn",schemaPath:"#/properties/finn/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.linkedin !== undefined){
const _errs7 = errors;
if(typeof data.linkedin !== "boolean"){
validate185.errors = [{instancePath:instancePath+"/linkedin",schemaPath:"#/properties/linkedin/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs7 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
else {
validate185.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate185.errors = vErrors;
return errors === 0;
}
validate185.evaluated = {"props":{"nav":true,"employers":true,"finn":true,"linkedin":true},"dynamicProps":false,"dynamicItems":false};

const schema65 = {"type":"object","required":["times","min_interval_hours"],"properties":{"times":{"$ref":"#/$defs/ClockTimes"},"min_interval_hours":{"type":"integer","const":2}}};
const schema66 = {"type":"array","items":{"$ref":"#/$defs/ClockTime"},"uniqueItems":true,"maxItems":12};
const schema67 = {"type":"string","pattern":"^([01][0-9]|2[0-3]):[0-5][0-9]$","minLength":5,"maxLength":5};
const pattern8 = new RegExp("^([01][0-9]|2[0-3]):[0-5][0-9]$", "u");

function validate189(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate189.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(typeof data === "string"){
if(func1(data) > 5){
validate189.errors = [{instancePath,schemaPath:"#/maxLength",keyword:"maxLength",params:{limit: 5},message:"must NOT have more than 5 characters"}];
return false;
}
else {
if(func1(data) < 5){
validate189.errors = [{instancePath,schemaPath:"#/minLength",keyword:"minLength",params:{limit: 5},message:"must NOT have fewer than 5 characters"}];
return false;
}
else {
if(!pattern8.test(data)){
validate189.errors = [{instancePath,schemaPath:"#/pattern",keyword:"pattern",params:{pattern: "^([01][0-9]|2[0-3]):[0-5][0-9]$"},message:"must match pattern \""+"^([01][0-9]|2[0-3]):[0-5][0-9]$"+"\""}];
return false;
}
}
}
}
else {
validate189.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
validate189.errors = vErrors;
return errors === 0;
}
validate189.evaluated = {"dynamicProps":false,"dynamicItems":false};


function validate188(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate188.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(Array.isArray(data)){
if(data.length > 12){
validate188.errors = [{instancePath,schemaPath:"#/maxItems",keyword:"maxItems",params:{limit: 12},message:"must NOT have more than 12 items"}];
return false;
}
else {
var valid0 = true;
const len0 = data.length;
for(let i0=0; i0<len0; i0++){
const _errs1 = errors;
if(!(validate189(data[i0], {instancePath:instancePath+"/" + i0,parentData:data,parentDataProperty:i0,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate189.errors : vErrors.concat(validate189.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
if(!valid0){
break;
}
}
if(valid0){
let i1 = data.length;
let j0;
if(i1 > 1){
outer0:
for(;i1--;){
for(j0 = i1; j0--;){
if(func0(data[i1], data[j0])){
validate188.errors = [{instancePath,schemaPath:"#/uniqueItems",keyword:"uniqueItems",params:{i: i1, j: j0},message:"must NOT have duplicate items (items ## "+j0+" and "+i1+" are identical)"}];
return false;
break outer0;
}
}
}
}
}
}
}
else {
validate188.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "array"},message:"must be array"}];
return false;
}
}
validate188.errors = vErrors;
return errors === 0;
}
validate188.evaluated = {"items":true,"dynamicProps":false,"dynamicItems":false};


function validate187(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate187.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((data.times === undefined) && (missing0 = "times")) || ((data.min_interval_hours === undefined) && (missing0 = "min_interval_hours"))){
validate187.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.times !== undefined){
const _errs1 = errors;
if(!(validate188(data.times, {instancePath:instancePath+"/times",parentData:data,parentDataProperty:"times",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate188.errors : vErrors.concat(validate188.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.min_interval_hours !== undefined){
let data1 = data.min_interval_hours;
const _errs2 = errors;
if(!(((typeof data1 == "number") && (!(data1 % 1) && !isNaN(data1))) && (isFinite(data1)))){
validate187.errors = [{instancePath:instancePath+"/min_interval_hours",schemaPath:"#/properties/min_interval_hours/type",keyword:"type",params:{type: "integer"},message:"must be integer"}];
return false;
}
if(2 !== data1){
validate187.errors = [{instancePath:instancePath+"/min_interval_hours",schemaPath:"#/properties/min_interval_hours/const",keyword:"const",params:{allowedValue: 2},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
}
}
}
else {
validate187.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate187.errors = vErrors;
return errors === 0;
}
validate187.evaluated = {"props":{"times":true,"min_interval_hours":true},"dynamicProps":false,"dynamicItems":false};

const schema68 = {"type":"object","required":["website","email"],"properties":{"website":{"type":"boolean"},"email":{"$ref":"#/$defs/EmailNotify"}}};
const schema69 = {"type":"object","required":["on","address"],"properties":{"on":{"type":"boolean"},"address":{"anyOf":[{"$ref":"#/$defs/Email"},{"type":"null"}]}}};
const schema70 = {"type":"string","maxLength":254,"pattern":"^[A-Za-z0-9!#$%&'*+/=?^_`{|}~.-]+@[A-Za-z0-9-]+(\\.[A-Za-z0-9-]+)*\\.[A-Za-z]+$","not":{"pattern":"[^A-Za-z0-9!#$%&'*+/=?^_`{|}~.@-]"}};
const pattern9 = new RegExp("[^A-Za-z0-9!#$%&'*+/=?^_`{|}~.@-]", "u");
const pattern10 = new RegExp("^[A-Za-z0-9!#$%&'*+/=?^_`{|}~.-]+@[A-Za-z0-9-]+(\\.[A-Za-z0-9-]+)*\\.[A-Za-z]+$", "u");

function validate195(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate195.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
const _errs1 = errors;
const _errs2 = errors;
if(typeof data === "string"){
if(!pattern9.test(data)){
const err0 = {};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
}
var valid0 = _errs2 === errors;
if(valid0){
validate195.errors = [{instancePath,schemaPath:"#/not",keyword:"not",params:{},message:"must NOT be valid"}];
return false;
}
else {
errors = _errs1;
if(vErrors !== null){
if(_errs1){
vErrors.length = _errs1;
}
else {
vErrors = null;
}
}
}
if(errors === 0){
if(typeof data === "string"){
if(func1(data) > 254){
validate195.errors = [{instancePath,schemaPath:"#/maxLength",keyword:"maxLength",params:{limit: 254},message:"must NOT have more than 254 characters"}];
return false;
}
else {
if(!pattern10.test(data)){
validate195.errors = [{instancePath,schemaPath:"#/pattern",keyword:"pattern",params:{pattern: "^[A-Za-z0-9!#$%&'*+/=?^_`{|}~.-]+@[A-Za-z0-9-]+(\\.[A-Za-z0-9-]+)*\\.[A-Za-z]+$"},message:"must match pattern \""+"^[A-Za-z0-9!#$%&'*+/=?^_`{|}~.-]+@[A-Za-z0-9-]+(\\.[A-Za-z0-9-]+)*\\.[A-Za-z]+$"+"\""}];
return false;
}
}
}
else {
validate195.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
validate195.errors = vErrors;
return errors === 0;
}
validate195.evaluated = {"dynamicProps":false,"dynamicItems":false};


function validate194(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate194.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((data.on === undefined) && (missing0 = "on")) || ((data.address === undefined) && (missing0 = "address"))){
validate194.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.on !== undefined){
const _errs1 = errors;
if(typeof data.on !== "boolean"){
validate194.errors = [{instancePath:instancePath+"/on",schemaPath:"#/properties/on/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.address !== undefined){
let data1 = data.address;
const _errs3 = errors;
const _errs4 = errors;
let valid1 = false;
const _errs5 = errors;
if(!(validate195(data1, {instancePath:instancePath+"/address",parentData:data,parentDataProperty:"address",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate195.errors : vErrors.concat(validate195.errors);
errors = vErrors.length;
}
var _valid0 = _errs5 === errors;
valid1 = valid1 || _valid0;
const _errs6 = errors;
if(data1 !== null){
const err0 = {instancePath:instancePath+"/address",schemaPath:"#/properties/address/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
var _valid0 = _errs6 === errors;
valid1 = valid1 || _valid0;
if(!valid1){
const err1 = {instancePath:instancePath+"/address",schemaPath:"#/properties/address/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
validate194.errors = vErrors;
return false;
}
else {
errors = _errs4;
if(vErrors !== null){
if(_errs4){
vErrors.length = _errs4;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
}
}
}
else {
validate194.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate194.errors = vErrors;
return errors === 0;
}
validate194.evaluated = {"props":{"on":true,"address":true},"dynamicProps":false,"dynamicItems":false};


function validate193(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate193.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((data.website === undefined) && (missing0 = "website")) || ((data.email === undefined) && (missing0 = "email"))){
validate193.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.website !== undefined){
const _errs1 = errors;
if(typeof data.website !== "boolean"){
validate193.errors = [{instancePath:instancePath+"/website",schemaPath:"#/properties/website/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.email !== undefined){
const _errs3 = errors;
if(!(validate194(data.email, {instancePath:instancePath+"/email",parentData:data,parentDataProperty:"email",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate194.errors : vErrors.concat(validate194.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
}
}
}
else {
validate193.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate193.errors = vErrors;
return errors === 0;
}
validate193.evaluated = {"props":{"website":true,"email":true},"dynamicProps":false,"dynamicItems":false};

const schema71 = {"type":"object","required":["provider","model","key_hint","price","monthly_limit_usd"],"properties":{"provider":{"anyOf":[{"$ref":"#/$defs/ProviderId"},{"type":"null"}]},"model":{"anyOf":[{"$ref":"#/$defs/ModelId"},{"type":"null"}]},"key_hint":{"anyOf":[{"type":"string","maxLength":4},{"type":"null"}]},"price":{"$ref":"#/$defs/Price"},"monthly_limit_usd":{"anyOf":[{"$ref":"#/$defs/Money"},{"type":"null"}]}}};
const schema72 = {"type":"string","enum":["anthropic","openai","openrouter","ollama"]};

function validate200(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate200.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(typeof data !== "string"){
validate200.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
if(!((((data === "anthropic") || (data === "openai")) || (data === "openrouter")) || (data === "ollama"))){
validate200.errors = [{instancePath,schemaPath:"#/enum",keyword:"enum",params:{allowedValues: schema72.enum},message:"must be equal to one of the allowed values"}];
return false;
}
validate200.errors = vErrors;
return errors === 0;
}
validate200.evaluated = {"dynamicProps":false,"dynamicItems":false};

const schema73 = {"type":"string","minLength":1,"maxLength":120};

function validate202(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate202.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(typeof data === "string"){
if(func1(data) > 120){
validate202.errors = [{instancePath,schemaPath:"#/maxLength",keyword:"maxLength",params:{limit: 120},message:"must NOT have more than 120 characters"}];
return false;
}
else {
if(func1(data) < 1){
validate202.errors = [{instancePath,schemaPath:"#/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"}];
return false;
}
}
}
else {
validate202.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
validate202.errors = vErrors;
return errors === 0;
}
validate202.evaluated = {"dynamicProps":false,"dynamicItems":false};

const schema74 = {"type":"object","required":["input_per_mtok","output_per_mtok"],"properties":{"input_per_mtok":{"$ref":"#/$defs/Money"},"output_per_mtok":{"$ref":"#/$defs/Money"}}};

function validate204(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate204.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((data.input_per_mtok === undefined) && (missing0 = "input_per_mtok")) || ((data.output_per_mtok === undefined) && (missing0 = "output_per_mtok"))){
validate204.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.input_per_mtok !== undefined){
const _errs1 = errors;
if(!(validate63(data.input_per_mtok, {instancePath:instancePath+"/input_per_mtok",parentData:data,parentDataProperty:"input_per_mtok",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate63.errors : vErrors.concat(validate63.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.output_per_mtok !== undefined){
const _errs2 = errors;
if(!(validate63(data.output_per_mtok, {instancePath:instancePath+"/output_per_mtok",parentData:data,parentDataProperty:"output_per_mtok",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate63.errors : vErrors.concat(validate63.errors);
errors = vErrors.length;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
}
}
}
else {
validate204.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate204.errors = vErrors;
return errors === 0;
}
validate204.evaluated = {"props":{"input_per_mtok":true,"output_per_mtok":true},"dynamicProps":false,"dynamicItems":false};


function validate199(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate199.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((((data.provider === undefined) && (missing0 = "provider")) || ((data.model === undefined) && (missing0 = "model"))) || ((data.key_hint === undefined) && (missing0 = "key_hint"))) || ((data.price === undefined) && (missing0 = "price"))) || ((data.monthly_limit_usd === undefined) && (missing0 = "monthly_limit_usd"))){
validate199.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.provider !== undefined){
let data0 = data.provider;
const _errs1 = errors;
const _errs2 = errors;
let valid1 = false;
const _errs3 = errors;
if(!(validate200(data0, {instancePath:instancePath+"/provider",parentData:data,parentDataProperty:"provider",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate200.errors : vErrors.concat(validate200.errors);
errors = vErrors.length;
}
var _valid0 = _errs3 === errors;
valid1 = valid1 || _valid0;
const _errs4 = errors;
if(data0 !== null){
const err0 = {instancePath:instancePath+"/provider",schemaPath:"#/properties/provider/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
var _valid0 = _errs4 === errors;
valid1 = valid1 || _valid0;
if(!valid1){
const err1 = {instancePath:instancePath+"/provider",schemaPath:"#/properties/provider/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
validate199.errors = vErrors;
return false;
}
else {
errors = _errs2;
if(vErrors !== null){
if(_errs2){
vErrors.length = _errs2;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.model !== undefined){
let data1 = data.model;
const _errs6 = errors;
const _errs7 = errors;
let valid2 = false;
const _errs8 = errors;
if(!(validate202(data1, {instancePath:instancePath+"/model",parentData:data,parentDataProperty:"model",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate202.errors : vErrors.concat(validate202.errors);
errors = vErrors.length;
}
var _valid1 = _errs8 === errors;
valid2 = valid2 || _valid1;
const _errs9 = errors;
if(data1 !== null){
const err2 = {instancePath:instancePath+"/model",schemaPath:"#/properties/model/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err2];
}
else {
vErrors.push(err2);
}
errors++;
}
var _valid1 = _errs9 === errors;
valid2 = valid2 || _valid1;
if(!valid2){
const err3 = {instancePath:instancePath+"/model",schemaPath:"#/properties/model/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err3];
}
else {
vErrors.push(err3);
}
errors++;
validate199.errors = vErrors;
return false;
}
else {
errors = _errs7;
if(vErrors !== null){
if(_errs7){
vErrors.length = _errs7;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs6 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.key_hint !== undefined){
let data2 = data.key_hint;
const _errs11 = errors;
const _errs12 = errors;
let valid3 = false;
const _errs13 = errors;
if(errors === _errs13){
if(typeof data2 === "string"){
if(func1(data2) > 4){
const err4 = {instancePath:instancePath+"/key_hint",schemaPath:"#/properties/key_hint/anyOf/0/maxLength",keyword:"maxLength",params:{limit: 4},message:"must NOT have more than 4 characters"};
if(vErrors === null){
vErrors = [err4];
}
else {
vErrors.push(err4);
}
errors++;
}
}
else {
const err5 = {instancePath:instancePath+"/key_hint",schemaPath:"#/properties/key_hint/anyOf/0/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err5];
}
else {
vErrors.push(err5);
}
errors++;
}
}
var _valid2 = _errs13 === errors;
valid3 = valid3 || _valid2;
const _errs15 = errors;
if(data2 !== null){
const err6 = {instancePath:instancePath+"/key_hint",schemaPath:"#/properties/key_hint/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err6];
}
else {
vErrors.push(err6);
}
errors++;
}
var _valid2 = _errs15 === errors;
valid3 = valid3 || _valid2;
if(!valid3){
const err7 = {instancePath:instancePath+"/key_hint",schemaPath:"#/properties/key_hint/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err7];
}
else {
vErrors.push(err7);
}
errors++;
validate199.errors = vErrors;
return false;
}
else {
errors = _errs12;
if(vErrors !== null){
if(_errs12){
vErrors.length = _errs12;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs11 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.price !== undefined){
const _errs17 = errors;
if(!(validate204(data.price, {instancePath:instancePath+"/price",parentData:data,parentDataProperty:"price",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate204.errors : vErrors.concat(validate204.errors);
errors = vErrors.length;
}
var valid0 = _errs17 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.monthly_limit_usd !== undefined){
let data4 = data.monthly_limit_usd;
const _errs18 = errors;
const _errs19 = errors;
let valid4 = false;
const _errs20 = errors;
if(!(validate63(data4, {instancePath:instancePath+"/monthly_limit_usd",parentData:data,parentDataProperty:"monthly_limit_usd",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate63.errors : vErrors.concat(validate63.errors);
errors = vErrors.length;
}
var _valid3 = _errs20 === errors;
valid4 = valid4 || _valid3;
const _errs21 = errors;
if(data4 !== null){
const err8 = {instancePath:instancePath+"/monthly_limit_usd",schemaPath:"#/properties/monthly_limit_usd/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err8];
}
else {
vErrors.push(err8);
}
errors++;
}
var _valid3 = _errs21 === errors;
valid4 = valid4 || _valid3;
if(!valid4){
const err9 = {instancePath:instancePath+"/monthly_limit_usd",schemaPath:"#/properties/monthly_limit_usd/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err9];
}
else {
vErrors.push(err9);
}
errors++;
validate199.errors = vErrors;
return false;
}
else {
errors = _errs19;
if(vErrors !== null){
if(_errs19){
vErrors.length = _errs19;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs18 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
else {
validate199.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate199.errors = vErrors;
return errors === 0;
}
validate199.evaluated = {"props":{"provider":true,"model":true,"key_hint":true,"price":true,"monthly_limit_usd":true},"dynamicProps":false,"dynamicItems":false};

const schema75 = {"type":"string","enum":["en","nb"]};

function validate210(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate210.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(typeof data !== "string"){
validate210.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
if(!((data === "en") || (data === "nb"))){
validate210.errors = [{instancePath,schemaPath:"#/enum",keyword:"enum",params:{allowedValues: schema75.enum},message:"must be equal to one of the allowed values"}];
return false;
}
validate210.errors = vErrors;
return errors === 0;
}
validate210.evaluated = {"dynamicProps":false,"dynamicItems":false};


function validate166(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate166.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((((((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.roles === undefined) && (missing0 = "roles"))) || ((data.targets === undefined) && (missing0 = "targets"))) || ((data.sources === undefined) && (missing0 = "sources"))) || ((data.schedule === undefined) && (missing0 = "schedule"))) || ((data.notify === undefined) && (missing0 = "notify"))) || ((data.model === undefined) && (missing0 = "model"))) || ((data.language === undefined) && (missing0 = "language"))){
validate166.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("config.snapshot" !== data.type){
validate166.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "config.snapshot"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate166.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.roles !== undefined){
let data2 = data.roles;
const _errs3 = errors;
if(errors === _errs3){
if(Array.isArray(data2)){
if(data2.length > 9){
validate166.errors = [{instancePath:instancePath+"/roles",schemaPath:"#/properties/roles/maxItems",keyword:"maxItems",params:{limit: 9},message:"must NOT have more than 9 items"}];
return false;
}
else {
var valid1 = true;
const len0 = data2.length;
for(let i0=0; i0<len0; i0++){
const _errs5 = errors;
if(!(validate167(data2[i0], {instancePath:instancePath+"/roles/" + i0,parentData:data2,parentDataProperty:i0,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate167.errors : vErrors.concat(validate167.errors);
errors = vErrors.length;
}
var valid1 = _errs5 === errors;
if(!valid1){
break;
}
}
}
}
else {
validate166.errors = [{instancePath:instancePath+"/roles",schemaPath:"#/properties/roles/type",keyword:"type",params:{type: "array"},message:"must be array"}];
return false;
}
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.targets !== undefined){
const _errs6 = errors;
if(!(validate170(data.targets, {instancePath:instancePath+"/targets",parentData:data,parentDataProperty:"targets",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate170.errors : vErrors.concat(validate170.errors);
errors = vErrors.length;
}
var valid0 = _errs6 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.sources !== undefined){
const _errs7 = errors;
if(!(validate185(data.sources, {instancePath:instancePath+"/sources",parentData:data,parentDataProperty:"sources",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate185.errors : vErrors.concat(validate185.errors);
errors = vErrors.length;
}
var valid0 = _errs7 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.schedule !== undefined){
const _errs8 = errors;
if(!(validate187(data.schedule, {instancePath:instancePath+"/schedule",parentData:data,parentDataProperty:"schedule",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate187.errors : vErrors.concat(validate187.errors);
errors = vErrors.length;
}
var valid0 = _errs8 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.notify !== undefined){
const _errs9 = errors;
if(!(validate193(data.notify, {instancePath:instancePath+"/notify",parentData:data,parentDataProperty:"notify",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate193.errors : vErrors.concat(validate193.errors);
errors = vErrors.length;
}
var valid0 = _errs9 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.model !== undefined){
const _errs10 = errors;
if(!(validate199(data.model, {instancePath:instancePath+"/model",parentData:data,parentDataProperty:"model",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate199.errors : vErrors.concat(validate199.errors);
errors = vErrors.length;
}
var valid0 = _errs10 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.language !== undefined){
const _errs11 = errors;
if(!(validate210(data.language, {instancePath:instancePath+"/language",parentData:data,parentDataProperty:"language",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate210.errors : vErrors.concat(validate210.errors);
errors = vErrors.length;
}
var valid0 = _errs11 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
}
}
}
else {
validate166.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate166.errors = vErrors;
return errors === 0;
}
validate166.evaluated = {"props":{"type":true,"v":true,"roles":true,"targets":true,"sources":true,"schedule":true,"notify":true,"model":true,"language":true},"dynamicProps":false,"dynamicItems":false};

const schema76 = {"type":"object","required":["type","v","cv_name","cv_read_at","summary","education","languages","stack","projects"],"properties":{"type":{"const":"profile.snapshot"},"v":{"const":1},"cv_name":{"anyOf":[{"type":"string","maxLength":120},{"type":"null"}]},"cv_read_at":{"anyOf":[{"$ref":"#/$defs/Timestamp"},{"type":"null"}]},"summary":{"$ref":"#/$defs/ShortText"},"education":{"$ref":"#/$defs/ShortText"},"languages":{"$ref":"#/$defs/Terms"},"stack":{"$ref":"#/$defs/Terms"},"projects":{"type":"array","items":{"$ref":"#/$defs/ProjectRef"},"maxItems":20}}};
const schema77 = {"type":"array","items":{"type":"string","minLength":1,"maxLength":60},"maxItems":40};

function validate216(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate216.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(Array.isArray(data)){
if(data.length > 40){
validate216.errors = [{instancePath,schemaPath:"#/maxItems",keyword:"maxItems",params:{limit: 40},message:"must NOT have more than 40 items"}];
return false;
}
else {
var valid0 = true;
const len0 = data.length;
for(let i0=0; i0<len0; i0++){
let data0 = data[i0];
const _errs1 = errors;
if(errors === _errs1){
if(typeof data0 === "string"){
if(func1(data0) > 60){
validate216.errors = [{instancePath:instancePath+"/" + i0,schemaPath:"#/items/maxLength",keyword:"maxLength",params:{limit: 60},message:"must NOT have more than 60 characters"}];
return false;
}
else {
if(func1(data0) < 1){
validate216.errors = [{instancePath:instancePath+"/" + i0,schemaPath:"#/items/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"}];
return false;
}
}
}
else {
validate216.errors = [{instancePath:instancePath+"/" + i0,schemaPath:"#/items/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs1 === errors;
if(!valid0){
break;
}
}
}
}
else {
validate216.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "array"},message:"must be array"}];
return false;
}
}
validate216.errors = vErrors;
return errors === 0;
}
validate216.evaluated = {"items":true,"dynamicProps":false,"dynamicItems":false};

const schema78 = {"type":"object","required":["id","name"],"properties":{"id":{"type":"string","minLength":1,"maxLength":40},"name":{"type":"string","minLength":1,"maxLength":120}}};

function validate219(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate219.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((data.id === undefined) && (missing0 = "id")) || ((data.name === undefined) && (missing0 = "name"))){
validate219.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.id !== undefined){
let data0 = data.id;
const _errs1 = errors;
if(errors === _errs1){
if(typeof data0 === "string"){
if(func1(data0) > 40){
validate219.errors = [{instancePath:instancePath+"/id",schemaPath:"#/properties/id/maxLength",keyword:"maxLength",params:{limit: 40},message:"must NOT have more than 40 characters"}];
return false;
}
else {
if(func1(data0) < 1){
validate219.errors = [{instancePath:instancePath+"/id",schemaPath:"#/properties/id/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"}];
return false;
}
}
}
else {
validate219.errors = [{instancePath:instancePath+"/id",schemaPath:"#/properties/id/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.name !== undefined){
let data1 = data.name;
const _errs3 = errors;
if(errors === _errs3){
if(typeof data1 === "string"){
if(func1(data1) > 120){
validate219.errors = [{instancePath:instancePath+"/name",schemaPath:"#/properties/name/maxLength",keyword:"maxLength",params:{limit: 120},message:"must NOT have more than 120 characters"}];
return false;
}
else {
if(func1(data1) < 1){
validate219.errors = [{instancePath:instancePath+"/name",schemaPath:"#/properties/name/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"}];
return false;
}
}
}
else {
validate219.errors = [{instancePath:instancePath+"/name",schemaPath:"#/properties/name/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
}
}
}
else {
validate219.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate219.errors = vErrors;
return errors === 0;
}
validate219.evaluated = {"props":{"id":true,"name":true},"dynamicProps":false,"dynamicItems":false};


function validate212(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate212.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((((((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.cv_name === undefined) && (missing0 = "cv_name"))) || ((data.cv_read_at === undefined) && (missing0 = "cv_read_at"))) || ((data.summary === undefined) && (missing0 = "summary"))) || ((data.education === undefined) && (missing0 = "education"))) || ((data.languages === undefined) && (missing0 = "languages"))) || ((data.stack === undefined) && (missing0 = "stack"))) || ((data.projects === undefined) && (missing0 = "projects"))){
validate212.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("profile.snapshot" !== data.type){
validate212.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "profile.snapshot"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate212.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.cv_name !== undefined){
let data2 = data.cv_name;
const _errs3 = errors;
const _errs4 = errors;
let valid1 = false;
const _errs5 = errors;
if(errors === _errs5){
if(typeof data2 === "string"){
if(func1(data2) > 120){
const err0 = {instancePath:instancePath+"/cv_name",schemaPath:"#/properties/cv_name/anyOf/0/maxLength",keyword:"maxLength",params:{limit: 120},message:"must NOT have more than 120 characters"};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
}
else {
const err1 = {instancePath:instancePath+"/cv_name",schemaPath:"#/properties/cv_name/anyOf/0/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
}
}
var _valid0 = _errs5 === errors;
valid1 = valid1 || _valid0;
const _errs7 = errors;
if(data2 !== null){
const err2 = {instancePath:instancePath+"/cv_name",schemaPath:"#/properties/cv_name/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err2];
}
else {
vErrors.push(err2);
}
errors++;
}
var _valid0 = _errs7 === errors;
valid1 = valid1 || _valid0;
if(!valid1){
const err3 = {instancePath:instancePath+"/cv_name",schemaPath:"#/properties/cv_name/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err3];
}
else {
vErrors.push(err3);
}
errors++;
validate212.errors = vErrors;
return false;
}
else {
errors = _errs4;
if(vErrors !== null){
if(_errs4){
vErrors.length = _errs4;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.cv_read_at !== undefined){
let data3 = data.cv_read_at;
const _errs9 = errors;
const _errs10 = errors;
let valid2 = false;
const _errs11 = errors;
if(!(validate54(data3, {instancePath:instancePath+"/cv_read_at",parentData:data,parentDataProperty:"cv_read_at",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate54.errors : vErrors.concat(validate54.errors);
errors = vErrors.length;
}
var _valid1 = _errs11 === errors;
valid2 = valid2 || _valid1;
const _errs12 = errors;
if(data3 !== null){
const err4 = {instancePath:instancePath+"/cv_read_at",schemaPath:"#/properties/cv_read_at/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err4];
}
else {
vErrors.push(err4);
}
errors++;
}
var _valid1 = _errs12 === errors;
valid2 = valid2 || _valid1;
if(!valid2){
const err5 = {instancePath:instancePath+"/cv_read_at",schemaPath:"#/properties/cv_read_at/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err5];
}
else {
vErrors.push(err5);
}
errors++;
validate212.errors = vErrors;
return false;
}
else {
errors = _errs10;
if(vErrors !== null){
if(_errs10){
vErrors.length = _errs10;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs9 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.summary !== undefined){
const _errs14 = errors;
if(!(validate98(data.summary, {instancePath:instancePath+"/summary",parentData:data,parentDataProperty:"summary",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate98.errors : vErrors.concat(validate98.errors);
errors = vErrors.length;
}
var valid0 = _errs14 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.education !== undefined){
const _errs15 = errors;
if(!(validate98(data.education, {instancePath:instancePath+"/education",parentData:data,parentDataProperty:"education",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate98.errors : vErrors.concat(validate98.errors);
errors = vErrors.length;
}
var valid0 = _errs15 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.languages !== undefined){
const _errs16 = errors;
if(!(validate216(data.languages, {instancePath:instancePath+"/languages",parentData:data,parentDataProperty:"languages",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate216.errors : vErrors.concat(validate216.errors);
errors = vErrors.length;
}
var valid0 = _errs16 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.stack !== undefined){
const _errs17 = errors;
if(!(validate216(data.stack, {instancePath:instancePath+"/stack",parentData:data,parentDataProperty:"stack",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate216.errors : vErrors.concat(validate216.errors);
errors = vErrors.length;
}
var valid0 = _errs17 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.projects !== undefined){
let data8 = data.projects;
const _errs18 = errors;
if(errors === _errs18){
if(Array.isArray(data8)){
if(data8.length > 20){
validate212.errors = [{instancePath:instancePath+"/projects",schemaPath:"#/properties/projects/maxItems",keyword:"maxItems",params:{limit: 20},message:"must NOT have more than 20 items"}];
return false;
}
else {
var valid3 = true;
const len0 = data8.length;
for(let i0=0; i0<len0; i0++){
const _errs20 = errors;
if(!(validate219(data8[i0], {instancePath:instancePath+"/projects/" + i0,parentData:data8,parentDataProperty:i0,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate219.errors : vErrors.concat(validate219.errors);
errors = vErrors.length;
}
var valid3 = _errs20 === errors;
if(!valid3){
break;
}
}
}
}
else {
validate212.errors = [{instancePath:instancePath+"/projects",schemaPath:"#/properties/projects/type",keyword:"type",params:{type: "array"},message:"must be array"}];
return false;
}
}
var valid0 = _errs18 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
}
}
}
else {
validate212.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate212.errors = vErrors;
return errors === 0;
}
validate212.evaluated = {"props":{"type":true,"v":true,"cv_name":true,"cv_read_at":true,"summary":true,"education":true,"languages":true,"stack":true,"projects":true},"dynamicProps":false,"dynamicItems":false};

const schema79 = {"type":"object","required":["type","v","month","month_tokens","today_tokens","month_usd","today_usd","resets_on","days"],"properties":{"type":{"const":"usage.snapshot"},"v":{"const":1},"month":{"type":"string","pattern":"^[0-9]{4}-(0[1-9]|1[0-2])$","not":{"pattern":"^0000-"},"minLength":7,"maxLength":7},"month_tokens":{"$ref":"#/$defs/Count"},"today_tokens":{"$ref":"#/$defs/Count"},"month_usd":{"$ref":"#/$defs/Money"},"today_usd":{"$ref":"#/$defs/Money"},"resets_on":{"$ref":"#/$defs/Day"},"days":{"type":"array","items":{"$ref":"#/$defs/UsageDay"},"maxItems":14}}};
const pattern12 = new RegExp("^[0-9]{4}-(0[1-9]|1[0-2])$", "u");
const schema80 = {"type":"object","required":["day","calls","input_tokens","output_tokens","cost_usd","ads_read","ads_checked"],"properties":{"day":{"$ref":"#/$defs/Day"},"calls":{"$ref":"#/$defs/Count"},"input_tokens":{"$ref":"#/$defs/Count"},"output_tokens":{"$ref":"#/$defs/Count"},"cost_usd":{"$ref":"#/$defs/Money"},"ads_read":{"$ref":"#/$defs/Count"},"ads_checked":{"$ref":"#/$defs/Count"}}};

function validate227(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate227.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((((((data.day === undefined) && (missing0 = "day")) || ((data.calls === undefined) && (missing0 = "calls"))) || ((data.input_tokens === undefined) && (missing0 = "input_tokens"))) || ((data.output_tokens === undefined) && (missing0 = "output_tokens"))) || ((data.cost_usd === undefined) && (missing0 = "cost_usd"))) || ((data.ads_read === undefined) && (missing0 = "ads_read"))) || ((data.ads_checked === undefined) && (missing0 = "ads_checked"))){
validate227.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.day !== undefined){
const _errs1 = errors;
if(!(validate76(data.day, {instancePath:instancePath+"/day",parentData:data,parentDataProperty:"day",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate76.errors : vErrors.concat(validate76.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.calls !== undefined){
const _errs2 = errors;
if(!(validate60(data.calls, {instancePath:instancePath+"/calls",parentData:data,parentDataProperty:"calls",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.input_tokens !== undefined){
const _errs3 = errors;
if(!(validate60(data.input_tokens, {instancePath:instancePath+"/input_tokens",parentData:data,parentDataProperty:"input_tokens",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.output_tokens !== undefined){
const _errs4 = errors;
if(!(validate60(data.output_tokens, {instancePath:instancePath+"/output_tokens",parentData:data,parentDataProperty:"output_tokens",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.cost_usd !== undefined){
const _errs5 = errors;
if(!(validate63(data.cost_usd, {instancePath:instancePath+"/cost_usd",parentData:data,parentDataProperty:"cost_usd",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate63.errors : vErrors.concat(validate63.errors);
errors = vErrors.length;
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.ads_read !== undefined){
const _errs6 = errors;
if(!(validate60(data.ads_read, {instancePath:instancePath+"/ads_read",parentData:data,parentDataProperty:"ads_read",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs6 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.ads_checked !== undefined){
const _errs7 = errors;
if(!(validate60(data.ads_checked, {instancePath:instancePath+"/ads_checked",parentData:data,parentDataProperty:"ads_checked",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs7 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
}
else {
validate227.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate227.errors = vErrors;
return errors === 0;
}
validate227.evaluated = {"props":{"day":true,"calls":true,"input_tokens":true,"output_tokens":true,"cost_usd":true,"ads_read":true,"ads_checked":true},"dynamicProps":false,"dynamicItems":false};


function validate221(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate221.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((((((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.month === undefined) && (missing0 = "month"))) || ((data.month_tokens === undefined) && (missing0 = "month_tokens"))) || ((data.today_tokens === undefined) && (missing0 = "today_tokens"))) || ((data.month_usd === undefined) && (missing0 = "month_usd"))) || ((data.today_usd === undefined) && (missing0 = "today_usd"))) || ((data.resets_on === undefined) && (missing0 = "resets_on"))) || ((data.days === undefined) && (missing0 = "days"))){
validate221.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("usage.snapshot" !== data.type){
validate221.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "usage.snapshot"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate221.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.month !== undefined){
let data2 = data.month;
const _errs3 = errors;
const _errs5 = errors;
const _errs6 = errors;
if(typeof data2 === "string"){
if(!pattern5.test(data2)){
const err0 = {};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
}
var valid1 = _errs6 === errors;
if(valid1){
validate221.errors = [{instancePath:instancePath+"/month",schemaPath:"#/properties/month/not",keyword:"not",params:{},message:"must NOT be valid"}];
return false;
}
else {
errors = _errs5;
if(vErrors !== null){
if(_errs5){
vErrors.length = _errs5;
}
else {
vErrors = null;
}
}
}
if(errors === _errs3){
if(typeof data2 === "string"){
if(func1(data2) > 7){
validate221.errors = [{instancePath:instancePath+"/month",schemaPath:"#/properties/month/maxLength",keyword:"maxLength",params:{limit: 7},message:"must NOT have more than 7 characters"}];
return false;
}
else {
if(func1(data2) < 7){
validate221.errors = [{instancePath:instancePath+"/month",schemaPath:"#/properties/month/minLength",keyword:"minLength",params:{limit: 7},message:"must NOT have fewer than 7 characters"}];
return false;
}
else {
if(!pattern12.test(data2)){
validate221.errors = [{instancePath:instancePath+"/month",schemaPath:"#/properties/month/pattern",keyword:"pattern",params:{pattern: "^[0-9]{4}-(0[1-9]|1[0-2])$"},message:"must match pattern \""+"^[0-9]{4}-(0[1-9]|1[0-2])$"+"\""}];
return false;
}
}
}
}
else {
validate221.errors = [{instancePath:instancePath+"/month",schemaPath:"#/properties/month/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.month_tokens !== undefined){
const _errs7 = errors;
if(!(validate60(data.month_tokens, {instancePath:instancePath+"/month_tokens",parentData:data,parentDataProperty:"month_tokens",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs7 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.today_tokens !== undefined){
const _errs8 = errors;
if(!(validate60(data.today_tokens, {instancePath:instancePath+"/today_tokens",parentData:data,parentDataProperty:"today_tokens",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs8 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.month_usd !== undefined){
const _errs9 = errors;
if(!(validate63(data.month_usd, {instancePath:instancePath+"/month_usd",parentData:data,parentDataProperty:"month_usd",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate63.errors : vErrors.concat(validate63.errors);
errors = vErrors.length;
}
var valid0 = _errs9 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.today_usd !== undefined){
const _errs10 = errors;
if(!(validate63(data.today_usd, {instancePath:instancePath+"/today_usd",parentData:data,parentDataProperty:"today_usd",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate63.errors : vErrors.concat(validate63.errors);
errors = vErrors.length;
}
var valid0 = _errs10 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.resets_on !== undefined){
const _errs11 = errors;
if(!(validate76(data.resets_on, {instancePath:instancePath+"/resets_on",parentData:data,parentDataProperty:"resets_on",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate76.errors : vErrors.concat(validate76.errors);
errors = vErrors.length;
}
var valid0 = _errs11 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.days !== undefined){
let data8 = data.days;
const _errs12 = errors;
if(errors === _errs12){
if(Array.isArray(data8)){
if(data8.length > 14){
validate221.errors = [{instancePath:instancePath+"/days",schemaPath:"#/properties/days/maxItems",keyword:"maxItems",params:{limit: 14},message:"must NOT have more than 14 items"}];
return false;
}
else {
var valid2 = true;
const len0 = data8.length;
for(let i0=0; i0<len0; i0++){
const _errs14 = errors;
if(!(validate227(data8[i0], {instancePath:instancePath+"/days/" + i0,parentData:data8,parentDataProperty:i0,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate227.errors : vErrors.concat(validate227.errors);
errors = vErrors.length;
}
var valid2 = _errs14 === errors;
if(!valid2){
break;
}
}
}
}
else {
validate221.errors = [{instancePath:instancePath+"/days",schemaPath:"#/properties/days/type",keyword:"type",params:{type: "array"},message:"must be array"}];
return false;
}
}
var valid0 = _errs12 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
}
}
}
else {
validate221.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate221.errors = vErrors;
return errors === 0;
}
validate221.evaluated = {"props":{"type":true,"v":true,"month":true,"month_tokens":true,"today_tokens":true,"month_usd":true,"today_usd":true,"resets_on":true,"days":true},"dynamicProps":false,"dynamicItems":false};

const schema81 = {"type":"object","required":["type","v","providers"],"properties":{"type":{"const":"models.snapshot"},"v":{"const":1},"providers":{"type":"array","items":{"$ref":"#/$defs/ProviderModels"},"maxItems":4}}};
const schema82 = {"type":"object","required":["provider","error","models"],"properties":{"provider":{"$ref":"#/$defs/ProviderId"},"error":{"anyOf":[{"$ref":"#/$defs/ShortText"},{"type":"null"}]},"models":{"type":"array","items":{"$ref":"#/$defs/ModelInfo"},"maxItems":1000}}};
const schema83 = {"type":"object","required":["id","name","input_per_mtok","output_per_mtok","usd_per_day"],"properties":{"id":{"$ref":"#/$defs/ModelId"},"name":{"type":"string","minLength":1,"maxLength":120},"input_per_mtok":{"anyOf":[{"$ref":"#/$defs/Money"},{"type":"null"}]},"output_per_mtok":{"anyOf":[{"$ref":"#/$defs/Money"},{"type":"null"}]},"usd_per_day":{"anyOf":[{"$ref":"#/$defs/Money"},{"type":"null"}]}}};

function validate240(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate240.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((((data.id === undefined) && (missing0 = "id")) || ((data.name === undefined) && (missing0 = "name"))) || ((data.input_per_mtok === undefined) && (missing0 = "input_per_mtok"))) || ((data.output_per_mtok === undefined) && (missing0 = "output_per_mtok"))) || ((data.usd_per_day === undefined) && (missing0 = "usd_per_day"))){
validate240.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.id !== undefined){
const _errs1 = errors;
if(!(validate202(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate202.errors : vErrors.concat(validate202.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.name !== undefined){
let data1 = data.name;
const _errs2 = errors;
if(errors === _errs2){
if(typeof data1 === "string"){
if(func1(data1) > 120){
validate240.errors = [{instancePath:instancePath+"/name",schemaPath:"#/properties/name/maxLength",keyword:"maxLength",params:{limit: 120},message:"must NOT have more than 120 characters"}];
return false;
}
else {
if(func1(data1) < 1){
validate240.errors = [{instancePath:instancePath+"/name",schemaPath:"#/properties/name/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"}];
return false;
}
}
}
else {
validate240.errors = [{instancePath:instancePath+"/name",schemaPath:"#/properties/name/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.input_per_mtok !== undefined){
let data2 = data.input_per_mtok;
const _errs4 = errors;
const _errs5 = errors;
let valid1 = false;
const _errs6 = errors;
if(!(validate63(data2, {instancePath:instancePath+"/input_per_mtok",parentData:data,parentDataProperty:"input_per_mtok",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate63.errors : vErrors.concat(validate63.errors);
errors = vErrors.length;
}
var _valid0 = _errs6 === errors;
valid1 = valid1 || _valid0;
const _errs7 = errors;
if(data2 !== null){
const err0 = {instancePath:instancePath+"/input_per_mtok",schemaPath:"#/properties/input_per_mtok/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
var _valid0 = _errs7 === errors;
valid1 = valid1 || _valid0;
if(!valid1){
const err1 = {instancePath:instancePath+"/input_per_mtok",schemaPath:"#/properties/input_per_mtok/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
validate240.errors = vErrors;
return false;
}
else {
errors = _errs5;
if(vErrors !== null){
if(_errs5){
vErrors.length = _errs5;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.output_per_mtok !== undefined){
let data3 = data.output_per_mtok;
const _errs9 = errors;
const _errs10 = errors;
let valid2 = false;
const _errs11 = errors;
if(!(validate63(data3, {instancePath:instancePath+"/output_per_mtok",parentData:data,parentDataProperty:"output_per_mtok",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate63.errors : vErrors.concat(validate63.errors);
errors = vErrors.length;
}
var _valid1 = _errs11 === errors;
valid2 = valid2 || _valid1;
const _errs12 = errors;
if(data3 !== null){
const err2 = {instancePath:instancePath+"/output_per_mtok",schemaPath:"#/properties/output_per_mtok/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err2];
}
else {
vErrors.push(err2);
}
errors++;
}
var _valid1 = _errs12 === errors;
valid2 = valid2 || _valid1;
if(!valid2){
const err3 = {instancePath:instancePath+"/output_per_mtok",schemaPath:"#/properties/output_per_mtok/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err3];
}
else {
vErrors.push(err3);
}
errors++;
validate240.errors = vErrors;
return false;
}
else {
errors = _errs10;
if(vErrors !== null){
if(_errs10){
vErrors.length = _errs10;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs9 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.usd_per_day !== undefined){
let data4 = data.usd_per_day;
const _errs14 = errors;
const _errs15 = errors;
let valid3 = false;
const _errs16 = errors;
if(!(validate63(data4, {instancePath:instancePath+"/usd_per_day",parentData:data,parentDataProperty:"usd_per_day",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate63.errors : vErrors.concat(validate63.errors);
errors = vErrors.length;
}
var _valid2 = _errs16 === errors;
valid3 = valid3 || _valid2;
const _errs17 = errors;
if(data4 !== null){
const err4 = {instancePath:instancePath+"/usd_per_day",schemaPath:"#/properties/usd_per_day/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err4];
}
else {
vErrors.push(err4);
}
errors++;
}
var _valid2 = _errs17 === errors;
valid3 = valid3 || _valid2;
if(!valid3){
const err5 = {instancePath:instancePath+"/usd_per_day",schemaPath:"#/properties/usd_per_day/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err5];
}
else {
vErrors.push(err5);
}
errors++;
validate240.errors = vErrors;
return false;
}
else {
errors = _errs15;
if(vErrors !== null){
if(_errs15){
vErrors.length = _errs15;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs14 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
else {
validate240.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate240.errors = vErrors;
return errors === 0;
}
validate240.evaluated = {"props":{"id":true,"name":true,"input_per_mtok":true,"output_per_mtok":true,"usd_per_day":true},"dynamicProps":false,"dynamicItems":false};


function validate237(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate237.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((data.provider === undefined) && (missing0 = "provider")) || ((data.error === undefined) && (missing0 = "error"))) || ((data.models === undefined) && (missing0 = "models"))){
validate237.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.provider !== undefined){
const _errs1 = errors;
if(!(validate200(data.provider, {instancePath:instancePath+"/provider",parentData:data,parentDataProperty:"provider",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate200.errors : vErrors.concat(validate200.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.error !== undefined){
let data1 = data.error;
const _errs2 = errors;
const _errs3 = errors;
let valid1 = false;
const _errs4 = errors;
if(!(validate98(data1, {instancePath:instancePath+"/error",parentData:data,parentDataProperty:"error",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate98.errors : vErrors.concat(validate98.errors);
errors = vErrors.length;
}
var _valid0 = _errs4 === errors;
valid1 = valid1 || _valid0;
const _errs5 = errors;
if(data1 !== null){
const err0 = {instancePath:instancePath+"/error",schemaPath:"#/properties/error/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
var _valid0 = _errs5 === errors;
valid1 = valid1 || _valid0;
if(!valid1){
const err1 = {instancePath:instancePath+"/error",schemaPath:"#/properties/error/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
validate237.errors = vErrors;
return false;
}
else {
errors = _errs3;
if(vErrors !== null){
if(_errs3){
vErrors.length = _errs3;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.models !== undefined){
let data2 = data.models;
const _errs7 = errors;
if(errors === _errs7){
if(Array.isArray(data2)){
if(data2.length > 1000){
validate237.errors = [{instancePath:instancePath+"/models",schemaPath:"#/properties/models/maxItems",keyword:"maxItems",params:{limit: 1000},message:"must NOT have more than 1000 items"}];
return false;
}
else {
var valid2 = true;
const len0 = data2.length;
for(let i0=0; i0<len0; i0++){
const _errs9 = errors;
if(!(validate240(data2[i0], {instancePath:instancePath+"/models/" + i0,parentData:data2,parentDataProperty:i0,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate240.errors : vErrors.concat(validate240.errors);
errors = vErrors.length;
}
var valid2 = _errs9 === errors;
if(!valid2){
break;
}
}
}
}
else {
validate237.errors = [{instancePath:instancePath+"/models",schemaPath:"#/properties/models/type",keyword:"type",params:{type: "array"},message:"must be array"}];
return false;
}
}
var valid0 = _errs7 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
else {
validate237.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate237.errors = vErrors;
return errors === 0;
}
validate237.evaluated = {"props":{"provider":true,"error":true,"models":true},"dynamicProps":false,"dynamicItems":false};


function validate236(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate236.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.providers === undefined) && (missing0 = "providers"))){
validate236.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("models.snapshot" !== data.type){
validate236.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "models.snapshot"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate236.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.providers !== undefined){
let data2 = data.providers;
const _errs3 = errors;
if(errors === _errs3){
if(Array.isArray(data2)){
if(data2.length > 4){
validate236.errors = [{instancePath:instancePath+"/providers",schemaPath:"#/properties/providers/maxItems",keyword:"maxItems",params:{limit: 4},message:"must NOT have more than 4 items"}];
return false;
}
else {
var valid1 = true;
const len0 = data2.length;
for(let i0=0; i0<len0; i0++){
const _errs5 = errors;
if(!(validate237(data2[i0], {instancePath:instancePath+"/providers/" + i0,parentData:data2,parentDataProperty:i0,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate237.errors : vErrors.concat(validate237.errors);
errors = vErrors.length;
}
var valid1 = _errs5 === errors;
if(!valid1){
break;
}
}
}
}
else {
validate236.errors = [{instancePath:instancePath+"/providers",schemaPath:"#/properties/providers/type",keyword:"type",params:{type: "array"},message:"must be array"}];
return false;
}
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
else {
validate236.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate236.errors = vErrors;
return errors === 0;
}
validate236.evaluated = {"props":{"type":true,"v":true,"providers":true},"dynamicProps":false,"dynamicItems":false};

const schema84 = {"type":"object","required":["type","v","text","is_default","limit"],"properties":{"type":{"const":"guidance.snapshot"},"v":{"const":1},"text":{"type":"string","maxLength":12000},"is_default":{"type":"boolean"},"limit":{"$ref":"#/$defs/Limit"}}};
const schema85 = {"type":"integer","minimum":1,"maximum":9007199254740991};

function validate248(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate248.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(!(((typeof data == "number") && (!(data % 1) && !isNaN(data))) && (isFinite(data)))){
validate248.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "integer"},message:"must be integer"}];
return false;
}
if(errors === 0){
if((typeof data == "number") && (isFinite(data))){
if(data > 9007199254740991 || isNaN(data)){
validate248.errors = [{instancePath,schemaPath:"#/maximum",keyword:"maximum",params:{comparison: "<=", limit: 9007199254740991},message:"must be <= 9007199254740991"}];
return false;
}
else {
if(data < 1 || isNaN(data)){
validate248.errors = [{instancePath,schemaPath:"#/minimum",keyword:"minimum",params:{comparison: ">=", limit: 1},message:"must be >= 1"}];
return false;
}
}
}
}
validate248.errors = vErrors;
return errors === 0;
}
validate248.evaluated = {"dynamicProps":false,"dynamicItems":false};


function validate247(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate247.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.text === undefined) && (missing0 = "text"))) || ((data.is_default === undefined) && (missing0 = "is_default"))) || ((data.limit === undefined) && (missing0 = "limit"))){
validate247.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("guidance.snapshot" !== data.type){
validate247.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "guidance.snapshot"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate247.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.text !== undefined){
let data2 = data.text;
const _errs3 = errors;
if(errors === _errs3){
if(typeof data2 === "string"){
if(func1(data2) > 12000){
validate247.errors = [{instancePath:instancePath+"/text",schemaPath:"#/properties/text/maxLength",keyword:"maxLength",params:{limit: 12000},message:"must NOT have more than 12000 characters"}];
return false;
}
}
else {
validate247.errors = [{instancePath:instancePath+"/text",schemaPath:"#/properties/text/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.is_default !== undefined){
const _errs5 = errors;
if(typeof data.is_default !== "boolean"){
validate247.errors = [{instancePath:instancePath+"/is_default",schemaPath:"#/properties/is_default/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.limit !== undefined){
const _errs7 = errors;
if(!(validate248(data.limit, {instancePath:instancePath+"/limit",parentData:data,parentDataProperty:"limit",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate248.errors : vErrors.concat(validate248.errors);
errors = vErrors.length;
}
var valid0 = _errs7 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
else {
validate247.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate247.errors = vErrors;
return errors === 0;
}
validate247.evaluated = {"props":{"type":true,"v":true,"text":true,"is_default":true,"limit":true},"dynamicProps":false,"dynamicItems":false};

const schema86 = {"type":"object","required":["type","v","about","about_limit","docs","docs_limit"],"properties":{"type":{"const":"context.snapshot"},"v":{"const":1},"about":{"type":"string","maxLength":4000},"about_limit":{"$ref":"#/$defs/Limit"},"docs":{"type":"array","items":{"$ref":"#/$defs/ContextDoc"},"maxItems":10},"docs_limit":{"$ref":"#/$defs/Limit"}}};
const schema87 = {"type":"object","required":["id","name","chars","added_at"],"properties":{"id":{"$ref":"#/$defs/DocId"},"name":{"type":"string","minLength":1,"maxLength":120},"chars":{"$ref":"#/$defs/Count"},"added_at":{"$ref":"#/$defs/Day"}}};
const schema88 = {"type":"string","pattern":"^doc_[a-z0-9_]+$","maxLength":64,"not":{"pattern":"[^a-z0-9_]"}};
const pattern13 = new RegExp("[^a-z0-9_]", "u");
const pattern14 = new RegExp("^doc_[a-z0-9_]+$", "u");

function validate253(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate253.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
const _errs1 = errors;
const _errs2 = errors;
if(typeof data === "string"){
if(!pattern13.test(data)){
const err0 = {};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
}
var valid0 = _errs2 === errors;
if(valid0){
validate253.errors = [{instancePath,schemaPath:"#/not",keyword:"not",params:{},message:"must NOT be valid"}];
return false;
}
else {
errors = _errs1;
if(vErrors !== null){
if(_errs1){
vErrors.length = _errs1;
}
else {
vErrors = null;
}
}
}
if(errors === 0){
if(typeof data === "string"){
if(func1(data) > 64){
validate253.errors = [{instancePath,schemaPath:"#/maxLength",keyword:"maxLength",params:{limit: 64},message:"must NOT have more than 64 characters"}];
return false;
}
else {
if(!pattern14.test(data)){
validate253.errors = [{instancePath,schemaPath:"#/pattern",keyword:"pattern",params:{pattern: "^doc_[a-z0-9_]+$"},message:"must match pattern \""+"^doc_[a-z0-9_]+$"+"\""}];
return false;
}
}
}
else {
validate253.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
validate253.errors = vErrors;
return errors === 0;
}
validate253.evaluated = {"dynamicProps":false,"dynamicItems":false};


function validate252(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate252.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((data.id === undefined) && (missing0 = "id")) || ((data.name === undefined) && (missing0 = "name"))) || ((data.chars === undefined) && (missing0 = "chars"))) || ((data.added_at === undefined) && (missing0 = "added_at"))){
validate252.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.id !== undefined){
const _errs1 = errors;
if(!(validate253(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate253.errors : vErrors.concat(validate253.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.name !== undefined){
let data1 = data.name;
const _errs2 = errors;
if(errors === _errs2){
if(typeof data1 === "string"){
if(func1(data1) > 120){
validate252.errors = [{instancePath:instancePath+"/name",schemaPath:"#/properties/name/maxLength",keyword:"maxLength",params:{limit: 120},message:"must NOT have more than 120 characters"}];
return false;
}
else {
if(func1(data1) < 1){
validate252.errors = [{instancePath:instancePath+"/name",schemaPath:"#/properties/name/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"}];
return false;
}
}
}
else {
validate252.errors = [{instancePath:instancePath+"/name",schemaPath:"#/properties/name/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.chars !== undefined){
const _errs4 = errors;
if(!(validate60(data.chars, {instancePath:instancePath+"/chars",parentData:data,parentDataProperty:"chars",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.added_at !== undefined){
const _errs5 = errors;
if(!(validate76(data.added_at, {instancePath:instancePath+"/added_at",parentData:data,parentDataProperty:"added_at",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate76.errors : vErrors.concat(validate76.errors);
errors = vErrors.length;
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
else {
validate252.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate252.errors = vErrors;
return errors === 0;
}
validate252.evaluated = {"props":{"id":true,"name":true,"chars":true,"added_at":true},"dynamicProps":false,"dynamicItems":false};


function validate250(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate250.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.about === undefined) && (missing0 = "about"))) || ((data.about_limit === undefined) && (missing0 = "about_limit"))) || ((data.docs === undefined) && (missing0 = "docs"))) || ((data.docs_limit === undefined) && (missing0 = "docs_limit"))){
validate250.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("context.snapshot" !== data.type){
validate250.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "context.snapshot"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate250.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.about !== undefined){
let data2 = data.about;
const _errs3 = errors;
if(errors === _errs3){
if(typeof data2 === "string"){
if(func1(data2) > 4000){
validate250.errors = [{instancePath:instancePath+"/about",schemaPath:"#/properties/about/maxLength",keyword:"maxLength",params:{limit: 4000},message:"must NOT have more than 4000 characters"}];
return false;
}
}
else {
validate250.errors = [{instancePath:instancePath+"/about",schemaPath:"#/properties/about/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.about_limit !== undefined){
const _errs5 = errors;
if(!(validate248(data.about_limit, {instancePath:instancePath+"/about_limit",parentData:data,parentDataProperty:"about_limit",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate248.errors : vErrors.concat(validate248.errors);
errors = vErrors.length;
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.docs !== undefined){
let data4 = data.docs;
const _errs6 = errors;
if(errors === _errs6){
if(Array.isArray(data4)){
if(data4.length > 10){
validate250.errors = [{instancePath:instancePath+"/docs",schemaPath:"#/properties/docs/maxItems",keyword:"maxItems",params:{limit: 10},message:"must NOT have more than 10 items"}];
return false;
}
else {
var valid1 = true;
const len0 = data4.length;
for(let i0=0; i0<len0; i0++){
const _errs8 = errors;
if(!(validate252(data4[i0], {instancePath:instancePath+"/docs/" + i0,parentData:data4,parentDataProperty:i0,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate252.errors : vErrors.concat(validate252.errors);
errors = vErrors.length;
}
var valid1 = _errs8 === errors;
if(!valid1){
break;
}
}
}
}
else {
validate250.errors = [{instancePath:instancePath+"/docs",schemaPath:"#/properties/docs/type",keyword:"type",params:{type: "array"},message:"must be array"}];
return false;
}
}
var valid0 = _errs6 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.docs_limit !== undefined){
const _errs9 = errors;
if(!(validate248(data.docs_limit, {instancePath:instancePath+"/docs_limit",parentData:data,parentDataProperty:"docs_limit",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate248.errors : vErrors.concat(validate248.errors);
errors = vErrors.length;
}
var valid0 = _errs9 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
else {
validate250.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate250.errors = vErrors;
return errors === 0;
}
validate250.evaluated = {"props":{"type":true,"v":true,"about":true,"about_limit":true,"docs":true,"docs_limit":true},"dynamicProps":false,"dynamicItems":false};

const schema89 = {"type":"object","required":["type","v","state","unavailable_reason","decision_count","cold_start_threshold","examples","examples_total"],"properties":{"type":{"const":"learning.snapshot"},"v":{"const":1},"state":{"type":"string","enum":["unavailable","collecting","preparing","on"]},"unavailable_reason":{"anyOf":[{"type":"string","maxLength":200},{"type":"null"}]},"decision_count":{"$ref":"#/$defs/Count"},"cold_start_threshold":{"type":"integer","const":20},"examples":{"type":"array","items":{"$ref":"#/$defs/LearningExample"},"maxItems":200},"examples_total":{"$ref":"#/$defs/Count"}}};
const schema90 = {"type":"object","required":["decision_id","ad_id","company","title","decision","at"],"properties":{"decision_id":{"$ref":"#/$defs/DecisionId"},"ad_id":{"$ref":"#/$defs/AdId"},"company":{"$ref":"#/$defs/Company"},"title":{"$ref":"#/$defs/AdTitle"},"decision":{"type":"string","enum":["applied","not_for_me","show_anyway"]},"at":{"$ref":"#/$defs/Timestamp"}}};
const schema91 = {"type":"integer","minimum":1,"maximum":9007199254740991};

function validate262(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate262.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(!(((typeof data == "number") && (!(data % 1) && !isNaN(data))) && (isFinite(data)))){
validate262.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "integer"},message:"must be integer"}];
return false;
}
if(errors === 0){
if((typeof data == "number") && (isFinite(data))){
if(data > 9007199254740991 || isNaN(data)){
validate262.errors = [{instancePath,schemaPath:"#/maximum",keyword:"maximum",params:{comparison: "<=", limit: 9007199254740991},message:"must be <= 9007199254740991"}];
return false;
}
else {
if(data < 1 || isNaN(data)){
validate262.errors = [{instancePath,schemaPath:"#/minimum",keyword:"minimum",params:{comparison: ">=", limit: 1},message:"must be >= 1"}];
return false;
}
}
}
}
validate262.errors = vErrors;
return errors === 0;
}
validate262.evaluated = {"dynamicProps":false,"dynamicItems":false};


function validate261(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate261.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((((data.decision_id === undefined) && (missing0 = "decision_id")) || ((data.ad_id === undefined) && (missing0 = "ad_id"))) || ((data.company === undefined) && (missing0 = "company"))) || ((data.title === undefined) && (missing0 = "title"))) || ((data.decision === undefined) && (missing0 = "decision"))) || ((data.at === undefined) && (missing0 = "at"))){
validate261.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.decision_id !== undefined){
const _errs1 = errors;
if(!(validate262(data.decision_id, {instancePath:instancePath+"/decision_id",parentData:data,parentDataProperty:"decision_id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate262.errors : vErrors.concat(validate262.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.ad_id !== undefined){
const _errs2 = errors;
if(!(validate79(data.ad_id, {instancePath:instancePath+"/ad_id",parentData:data,parentDataProperty:"ad_id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate79.errors : vErrors.concat(validate79.errors);
errors = vErrors.length;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.company !== undefined){
const _errs3 = errors;
if(!(validate81(data.company, {instancePath:instancePath+"/company",parentData:data,parentDataProperty:"company",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate81.errors : vErrors.concat(validate81.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.title !== undefined){
const _errs4 = errors;
if(!(validate83(data.title, {instancePath:instancePath+"/title",parentData:data,parentDataProperty:"title",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate83.errors : vErrors.concat(validate83.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.decision !== undefined){
let data4 = data.decision;
const _errs5 = errors;
if(typeof data4 !== "string"){
validate261.errors = [{instancePath:instancePath+"/decision",schemaPath:"#/properties/decision/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
if(!(((data4 === "applied") || (data4 === "not_for_me")) || (data4 === "show_anyway"))){
validate261.errors = [{instancePath:instancePath+"/decision",schemaPath:"#/properties/decision/enum",keyword:"enum",params:{allowedValues: schema90.properties.decision.enum},message:"must be equal to one of the allowed values"}];
return false;
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.at !== undefined){
const _errs7 = errors;
if(!(validate54(data.at, {instancePath:instancePath+"/at",parentData:data,parentDataProperty:"at",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate54.errors : vErrors.concat(validate54.errors);
errors = vErrors.length;
}
var valid0 = _errs7 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
else {
validate261.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate261.errors = vErrors;
return errors === 0;
}
validate261.evaluated = {"props":{"decision_id":true,"ad_id":true,"company":true,"title":true,"decision":true,"at":true},"dynamicProps":false,"dynamicItems":false};


function validate259(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate259.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.state === undefined) && (missing0 = "state"))) || ((data.unavailable_reason === undefined) && (missing0 = "unavailable_reason"))) || ((data.decision_count === undefined) && (missing0 = "decision_count"))) || ((data.cold_start_threshold === undefined) && (missing0 = "cold_start_threshold"))) || ((data.examples === undefined) && (missing0 = "examples"))) || ((data.examples_total === undefined) && (missing0 = "examples_total"))){
validate259.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("learning.snapshot" !== data.type){
validate259.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "learning.snapshot"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate259.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.state !== undefined){
let data2 = data.state;
const _errs3 = errors;
if(typeof data2 !== "string"){
validate259.errors = [{instancePath:instancePath+"/state",schemaPath:"#/properties/state/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
if(!((((data2 === "unavailable") || (data2 === "collecting")) || (data2 === "preparing")) || (data2 === "on"))){
validate259.errors = [{instancePath:instancePath+"/state",schemaPath:"#/properties/state/enum",keyword:"enum",params:{allowedValues: schema89.properties.state.enum},message:"must be equal to one of the allowed values"}];
return false;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.unavailable_reason !== undefined){
let data3 = data.unavailable_reason;
const _errs5 = errors;
const _errs6 = errors;
let valid1 = false;
const _errs7 = errors;
if(errors === _errs7){
if(typeof data3 === "string"){
if(func1(data3) > 200){
const err0 = {instancePath:instancePath+"/unavailable_reason",schemaPath:"#/properties/unavailable_reason/anyOf/0/maxLength",keyword:"maxLength",params:{limit: 200},message:"must NOT have more than 200 characters"};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
}
else {
const err1 = {instancePath:instancePath+"/unavailable_reason",schemaPath:"#/properties/unavailable_reason/anyOf/0/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
}
}
var _valid0 = _errs7 === errors;
valid1 = valid1 || _valid0;
const _errs9 = errors;
if(data3 !== null){
const err2 = {instancePath:instancePath+"/unavailable_reason",schemaPath:"#/properties/unavailable_reason/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err2];
}
else {
vErrors.push(err2);
}
errors++;
}
var _valid0 = _errs9 === errors;
valid1 = valid1 || _valid0;
if(!valid1){
const err3 = {instancePath:instancePath+"/unavailable_reason",schemaPath:"#/properties/unavailable_reason/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err3];
}
else {
vErrors.push(err3);
}
errors++;
validate259.errors = vErrors;
return false;
}
else {
errors = _errs6;
if(vErrors !== null){
if(_errs6){
vErrors.length = _errs6;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.decision_count !== undefined){
const _errs11 = errors;
if(!(validate60(data.decision_count, {instancePath:instancePath+"/decision_count",parentData:data,parentDataProperty:"decision_count",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs11 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.cold_start_threshold !== undefined){
let data5 = data.cold_start_threshold;
const _errs12 = errors;
if(!(((typeof data5 == "number") && (!(data5 % 1) && !isNaN(data5))) && (isFinite(data5)))){
validate259.errors = [{instancePath:instancePath+"/cold_start_threshold",schemaPath:"#/properties/cold_start_threshold/type",keyword:"type",params:{type: "integer"},message:"must be integer"}];
return false;
}
if(20 !== data5){
validate259.errors = [{instancePath:instancePath+"/cold_start_threshold",schemaPath:"#/properties/cold_start_threshold/const",keyword:"const",params:{allowedValue: 20},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs12 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.examples !== undefined){
let data6 = data.examples;
const _errs14 = errors;
if(errors === _errs14){
if(Array.isArray(data6)){
if(data6.length > 200){
validate259.errors = [{instancePath:instancePath+"/examples",schemaPath:"#/properties/examples/maxItems",keyword:"maxItems",params:{limit: 200},message:"must NOT have more than 200 items"}];
return false;
}
else {
var valid2 = true;
const len0 = data6.length;
for(let i0=0; i0<len0; i0++){
const _errs16 = errors;
if(!(validate261(data6[i0], {instancePath:instancePath+"/examples/" + i0,parentData:data6,parentDataProperty:i0,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate261.errors : vErrors.concat(validate261.errors);
errors = vErrors.length;
}
var valid2 = _errs16 === errors;
if(!valid2){
break;
}
}
}
}
else {
validate259.errors = [{instancePath:instancePath+"/examples",schemaPath:"#/properties/examples/type",keyword:"type",params:{type: "array"},message:"must be array"}];
return false;
}
}
var valid0 = _errs14 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.examples_total !== undefined){
const _errs17 = errors;
if(!(validate60(data.examples_total, {instancePath:instancePath+"/examples_total",parentData:data,parentDataProperty:"examples_total",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs17 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
}
}
else {
validate259.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate259.errors = vErrors;
return errors === 0;
}
validate259.evaluated = {"props":{"type":true,"v":true,"state":true,"unavailable_reason":true,"decision_count":true,"cold_start_threshold":true,"examples":true,"examples_total":true},"dynamicProps":false,"dynamicItems":false};

const schema92 = {"type":"object","required":["type","v","run_id","mode","started_at","percent","sources","model"],"properties":{"type":{"const":"run.progress"},"v":{"const":1},"run_id":{"$ref":"#/$defs/RunId"},"mode":{"$ref":"#/$defs/RunMode"},"started_at":{"$ref":"#/$defs/Timestamp"},"percent":{"type":"integer","minimum":0,"maximum":100},"sources":{"type":"array","items":{"$ref":"#/$defs/SourceStep"},"maxItems":5},"model":{"$ref":"#/$defs/ModelStep"}}};
const schema93 = {"type":"object","required":["source","state","found_new"],"properties":{"source":{"$ref":"#/$defs/SourceId"},"state":{"type":"string","enum":["pending","active","done","failed"]},"found_new":{"anyOf":[{"$ref":"#/$defs/Count"},{"type":"null"}]}}};

function validate274(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate274.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((data.source === undefined) && (missing0 = "source")) || ((data.state === undefined) && (missing0 = "state"))) || ((data.found_new === undefined) && (missing0 = "found_new"))){
validate274.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.source !== undefined){
const _errs1 = errors;
if(!(validate89(data.source, {instancePath:instancePath+"/source",parentData:data,parentDataProperty:"source",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate89.errors : vErrors.concat(validate89.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.state !== undefined){
let data1 = data.state;
const _errs2 = errors;
if(typeof data1 !== "string"){
validate274.errors = [{instancePath:instancePath+"/state",schemaPath:"#/properties/state/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
if(!((((data1 === "pending") || (data1 === "active")) || (data1 === "done")) || (data1 === "failed"))){
validate274.errors = [{instancePath:instancePath+"/state",schemaPath:"#/properties/state/enum",keyword:"enum",params:{allowedValues: schema93.properties.state.enum},message:"must be equal to one of the allowed values"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.found_new !== undefined){
let data2 = data.found_new;
const _errs4 = errors;
const _errs5 = errors;
let valid1 = false;
const _errs6 = errors;
if(!(validate60(data2, {instancePath:instancePath+"/found_new",parentData:data,parentDataProperty:"found_new",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var _valid0 = _errs6 === errors;
valid1 = valid1 || _valid0;
const _errs7 = errors;
if(data2 !== null){
const err0 = {instancePath:instancePath+"/found_new",schemaPath:"#/properties/found_new/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
var _valid0 = _errs7 === errors;
valid1 = valid1 || _valid0;
if(!valid1){
const err1 = {instancePath:instancePath+"/found_new",schemaPath:"#/properties/found_new/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
validate274.errors = vErrors;
return false;
}
else {
errors = _errs5;
if(vErrors !== null){
if(_errs5){
vErrors.length = _errs5;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
else {
validate274.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate274.errors = vErrors;
return errors === 0;
}
validate274.evaluated = {"props":{"source":true,"state":true,"found_new":true},"dynamicProps":false,"dynamicItems":false};

const schema94 = {"type":"object","required":["state","checked","total","results_by"],"properties":{"state":{"type":"string","enum":["pending","active","done","skipped"]},"checked":{"$ref":"#/$defs/Count"},"total":{"$ref":"#/$defs/Count"},"results_by":{"anyOf":[{"$ref":"#/$defs/Timestamp"},{"type":"null"}]}}};

function validate278(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate278.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((data.state === undefined) && (missing0 = "state")) || ((data.checked === undefined) && (missing0 = "checked"))) || ((data.total === undefined) && (missing0 = "total"))) || ((data.results_by === undefined) && (missing0 = "results_by"))){
validate278.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.state !== undefined){
let data0 = data.state;
const _errs1 = errors;
if(typeof data0 !== "string"){
validate278.errors = [{instancePath:instancePath+"/state",schemaPath:"#/properties/state/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
if(!((((data0 === "pending") || (data0 === "active")) || (data0 === "done")) || (data0 === "skipped"))){
validate278.errors = [{instancePath:instancePath+"/state",schemaPath:"#/properties/state/enum",keyword:"enum",params:{allowedValues: schema94.properties.state.enum},message:"must be equal to one of the allowed values"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.checked !== undefined){
const _errs3 = errors;
if(!(validate60(data.checked, {instancePath:instancePath+"/checked",parentData:data,parentDataProperty:"checked",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.total !== undefined){
const _errs4 = errors;
if(!(validate60(data.total, {instancePath:instancePath+"/total",parentData:data,parentDataProperty:"total",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.results_by !== undefined){
let data3 = data.results_by;
const _errs5 = errors;
const _errs6 = errors;
let valid1 = false;
const _errs7 = errors;
if(!(validate54(data3, {instancePath:instancePath+"/results_by",parentData:data,parentDataProperty:"results_by",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate54.errors : vErrors.concat(validate54.errors);
errors = vErrors.length;
}
var _valid0 = _errs7 === errors;
valid1 = valid1 || _valid0;
const _errs8 = errors;
if(data3 !== null){
const err0 = {instancePath:instancePath+"/results_by",schemaPath:"#/properties/results_by/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
var _valid0 = _errs8 === errors;
valid1 = valid1 || _valid0;
if(!valid1){
const err1 = {instancePath:instancePath+"/results_by",schemaPath:"#/properties/results_by/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
validate278.errors = vErrors;
return false;
}
else {
errors = _errs6;
if(vErrors !== null){
if(_errs6){
vErrors.length = _errs6;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
else {
validate278.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate278.errors = vErrors;
return errors === 0;
}
validate278.evaluated = {"props":{"state":true,"checked":true,"total":true,"results_by":true},"dynamicProps":false,"dynamicItems":false};


function validate270(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate270.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.run_id === undefined) && (missing0 = "run_id"))) || ((data.mode === undefined) && (missing0 = "mode"))) || ((data.started_at === undefined) && (missing0 = "started_at"))) || ((data.percent === undefined) && (missing0 = "percent"))) || ((data.sources === undefined) && (missing0 = "sources"))) || ((data.model === undefined) && (missing0 = "model"))){
validate270.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("run.progress" !== data.type){
validate270.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "run.progress"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate270.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.run_id !== undefined){
const _errs3 = errors;
if(!(validate57(data.run_id, {instancePath:instancePath+"/run_id",parentData:data,parentDataProperty:"run_id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate57.errors : vErrors.concat(validate57.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.mode !== undefined){
const _errs4 = errors;
if(!(validate69(data.mode, {instancePath:instancePath+"/mode",parentData:data,parentDataProperty:"mode",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate69.errors : vErrors.concat(validate69.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.started_at !== undefined){
const _errs5 = errors;
if(!(validate54(data.started_at, {instancePath:instancePath+"/started_at",parentData:data,parentDataProperty:"started_at",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate54.errors : vErrors.concat(validate54.errors);
errors = vErrors.length;
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.percent !== undefined){
let data5 = data.percent;
const _errs6 = errors;
if(!(((typeof data5 == "number") && (!(data5 % 1) && !isNaN(data5))) && (isFinite(data5)))){
validate270.errors = [{instancePath:instancePath+"/percent",schemaPath:"#/properties/percent/type",keyword:"type",params:{type: "integer"},message:"must be integer"}];
return false;
}
if(errors === _errs6){
if((typeof data5 == "number") && (isFinite(data5))){
if(data5 > 100 || isNaN(data5)){
validate270.errors = [{instancePath:instancePath+"/percent",schemaPath:"#/properties/percent/maximum",keyword:"maximum",params:{comparison: "<=", limit: 100},message:"must be <= 100"}];
return false;
}
else {
if(data5 < 0 || isNaN(data5)){
validate270.errors = [{instancePath:instancePath+"/percent",schemaPath:"#/properties/percent/minimum",keyword:"minimum",params:{comparison: ">=", limit: 0},message:"must be >= 0"}];
return false;
}
}
}
}
var valid0 = _errs6 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.sources !== undefined){
let data6 = data.sources;
const _errs8 = errors;
if(errors === _errs8){
if(Array.isArray(data6)){
if(data6.length > 5){
validate270.errors = [{instancePath:instancePath+"/sources",schemaPath:"#/properties/sources/maxItems",keyword:"maxItems",params:{limit: 5},message:"must NOT have more than 5 items"}];
return false;
}
else {
var valid1 = true;
const len0 = data6.length;
for(let i0=0; i0<len0; i0++){
const _errs10 = errors;
if(!(validate274(data6[i0], {instancePath:instancePath+"/sources/" + i0,parentData:data6,parentDataProperty:i0,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate274.errors : vErrors.concat(validate274.errors);
errors = vErrors.length;
}
var valid1 = _errs10 === errors;
if(!valid1){
break;
}
}
}
}
else {
validate270.errors = [{instancePath:instancePath+"/sources",schemaPath:"#/properties/sources/type",keyword:"type",params:{type: "array"},message:"must be array"}];
return false;
}
}
var valid0 = _errs8 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.model !== undefined){
const _errs11 = errors;
if(!(validate278(data.model, {instancePath:instancePath+"/model",parentData:data,parentDataProperty:"model",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate278.errors : vErrors.concat(validate278.errors);
errors = vErrors.length;
}
var valid0 = _errs11 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
}
}
else {
validate270.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate270.errors = vErrors;
return errors === 0;
}
validate270.evaluated = {"props":{"type":true,"v":true,"run_id":true,"mode":true,"started_at":true,"percent":true,"sources":true,"model":true},"dynamicProps":false,"dynamicItems":false};

const schema95 = {"type":"object","required":["type","v","run_id","finished_at","ads_read","new_ads","matches","hidden","cost_usd","failed_sources","budget_paused"],"properties":{"type":{"const":"run.finished"},"v":{"const":1},"run_id":{"$ref":"#/$defs/RunId"},"finished_at":{"$ref":"#/$defs/Timestamp"},"ads_read":{"$ref":"#/$defs/Count"},"new_ads":{"$ref":"#/$defs/Count"},"matches":{"$ref":"#/$defs/Count"},"hidden":{"$ref":"#/$defs/Count"},"cost_usd":{"$ref":"#/$defs/Money"},"failed_sources":{"type":"array","items":{"$ref":"#/$defs/SourceId"},"maxItems":5},"budget_paused":{"type":"boolean"}}};

function validate283(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate283.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((((((((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.run_id === undefined) && (missing0 = "run_id"))) || ((data.finished_at === undefined) && (missing0 = "finished_at"))) || ((data.ads_read === undefined) && (missing0 = "ads_read"))) || ((data.new_ads === undefined) && (missing0 = "new_ads"))) || ((data.matches === undefined) && (missing0 = "matches"))) || ((data.hidden === undefined) && (missing0 = "hidden"))) || ((data.cost_usd === undefined) && (missing0 = "cost_usd"))) || ((data.failed_sources === undefined) && (missing0 = "failed_sources"))) || ((data.budget_paused === undefined) && (missing0 = "budget_paused"))){
validate283.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("run.finished" !== data.type){
validate283.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "run.finished"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate283.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.run_id !== undefined){
const _errs3 = errors;
if(!(validate57(data.run_id, {instancePath:instancePath+"/run_id",parentData:data,parentDataProperty:"run_id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate57.errors : vErrors.concat(validate57.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.finished_at !== undefined){
const _errs4 = errors;
if(!(validate54(data.finished_at, {instancePath:instancePath+"/finished_at",parentData:data,parentDataProperty:"finished_at",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate54.errors : vErrors.concat(validate54.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.ads_read !== undefined){
const _errs5 = errors;
if(!(validate60(data.ads_read, {instancePath:instancePath+"/ads_read",parentData:data,parentDataProperty:"ads_read",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.new_ads !== undefined){
const _errs6 = errors;
if(!(validate60(data.new_ads, {instancePath:instancePath+"/new_ads",parentData:data,parentDataProperty:"new_ads",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs6 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.matches !== undefined){
const _errs7 = errors;
if(!(validate60(data.matches, {instancePath:instancePath+"/matches",parentData:data,parentDataProperty:"matches",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs7 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.hidden !== undefined){
const _errs8 = errors;
if(!(validate60(data.hidden, {instancePath:instancePath+"/hidden",parentData:data,parentDataProperty:"hidden",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs8 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.cost_usd !== undefined){
const _errs9 = errors;
if(!(validate63(data.cost_usd, {instancePath:instancePath+"/cost_usd",parentData:data,parentDataProperty:"cost_usd",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate63.errors : vErrors.concat(validate63.errors);
errors = vErrors.length;
}
var valid0 = _errs9 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.failed_sources !== undefined){
let data9 = data.failed_sources;
const _errs10 = errors;
if(errors === _errs10){
if(Array.isArray(data9)){
if(data9.length > 5){
validate283.errors = [{instancePath:instancePath+"/failed_sources",schemaPath:"#/properties/failed_sources/maxItems",keyword:"maxItems",params:{limit: 5},message:"must NOT have more than 5 items"}];
return false;
}
else {
var valid1 = true;
const len0 = data9.length;
for(let i0=0; i0<len0; i0++){
const _errs12 = errors;
if(!(validate89(data9[i0], {instancePath:instancePath+"/failed_sources/" + i0,parentData:data9,parentDataProperty:i0,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate89.errors : vErrors.concat(validate89.errors);
errors = vErrors.length;
}
var valid1 = _errs12 === errors;
if(!valid1){
break;
}
}
}
}
else {
validate283.errors = [{instancePath:instancePath+"/failed_sources",schemaPath:"#/properties/failed_sources/type",keyword:"type",params:{type: "array"},message:"must be array"}];
return false;
}
}
var valid0 = _errs10 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.budget_paused !== undefined){
const _errs13 = errors;
if(typeof data.budget_paused !== "boolean"){
validate283.errors = [{instancePath:instancePath+"/budget_paused",schemaPath:"#/properties/budget_paused/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs13 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
}
}
}
}
}
else {
validate283.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate283.errors = vErrors;
return errors === 0;
}
validate283.evaluated = {"props":{"type":true,"v":true,"run_id":true,"finished_at":true,"ads_read":true,"new_ads":true,"matches":true,"hidden":true,"cost_usd":true,"failed_sources":true,"budget_paused":true},"dynamicProps":false,"dynamicItems":false};

const schema96 = {"type":"object","required":["type","v","run_id","at"],"properties":{"type":{"const":"run.cancelled"},"v":{"const":1},"run_id":{"$ref":"#/$defs/RunId"},"at":{"$ref":"#/$defs/Timestamp"}}};

function validate292(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate292.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.run_id === undefined) && (missing0 = "run_id"))) || ((data.at === undefined) && (missing0 = "at"))){
validate292.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("run.cancelled" !== data.type){
validate292.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "run.cancelled"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate292.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.run_id !== undefined){
const _errs3 = errors;
if(!(validate57(data.run_id, {instancePath:instancePath+"/run_id",parentData:data,parentDataProperty:"run_id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate57.errors : vErrors.concat(validate57.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.at !== undefined){
const _errs4 = errors;
if(!(validate54(data.at, {instancePath:instancePath+"/at",parentData:data,parentDataProperty:"at",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate54.errors : vErrors.concat(validate54.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
else {
validate292.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate292.errors = vErrors;
return errors === 0;
}
validate292.evaluated = {"props":{"type":true,"v":true,"run_id":true,"at":true},"dynamicProps":false,"dynamicItems":false};

const schema97 = {"type":"object","required":["type","v","ref","applies_from"],"properties":{"type":{"const":"saved"},"v":{"const":1},"ref":{"$ref":"#/$defs/MessageId"},"applies_from":{"type":"string","enum":["now","next_run"]},"recheck":{"$ref":"#/$defs/Recheck"}}};
const schema98 = {"type":"string","pattern":"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$","not":{"pattern":"\\s"}};
const pattern16 = new RegExp("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", "u");

function validate296(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate296.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
const _errs1 = errors;
const _errs2 = errors;
if(typeof data === "string"){
if(!pattern3.test(data)){
const err0 = {};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
}
var valid0 = _errs2 === errors;
if(valid0){
validate296.errors = [{instancePath,schemaPath:"#/not",keyword:"not",params:{},message:"must NOT be valid"}];
return false;
}
else {
errors = _errs1;
if(vErrors !== null){
if(_errs1){
vErrors.length = _errs1;
}
else {
vErrors = null;
}
}
}
if(errors === 0){
if(typeof data === "string"){
if(!pattern16.test(data)){
validate296.errors = [{instancePath,schemaPath:"#/pattern",keyword:"pattern",params:{pattern: "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"},message:"must match pattern \""+"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"+"\""}];
return false;
}
}
else {
validate296.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
validate296.errors = vErrors;
return errors === 0;
}
validate296.evaluated = {"dynamicProps":false,"dynamicItems":false};

const schema99 = {"type":"object","required":["ads","cost_usd"],"properties":{"ads":{"$ref":"#/$defs/Count"},"cost_usd":{"anyOf":[{"$ref":"#/$defs/Money"},{"type":"null"}]}}};

function validate298(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate298.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((data.ads === undefined) && (missing0 = "ads")) || ((data.cost_usd === undefined) && (missing0 = "cost_usd"))){
validate298.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.ads !== undefined){
const _errs1 = errors;
if(!(validate60(data.ads, {instancePath:instancePath+"/ads",parentData:data,parentDataProperty:"ads",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate60.errors : vErrors.concat(validate60.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.cost_usd !== undefined){
let data1 = data.cost_usd;
const _errs2 = errors;
const _errs3 = errors;
let valid1 = false;
const _errs4 = errors;
if(!(validate63(data1, {instancePath:instancePath+"/cost_usd",parentData:data,parentDataProperty:"cost_usd",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate63.errors : vErrors.concat(validate63.errors);
errors = vErrors.length;
}
var _valid0 = _errs4 === errors;
valid1 = valid1 || _valid0;
const _errs5 = errors;
if(data1 !== null){
const err0 = {instancePath:instancePath+"/cost_usd",schemaPath:"#/properties/cost_usd/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
var _valid0 = _errs5 === errors;
valid1 = valid1 || _valid0;
if(!valid1){
const err1 = {instancePath:instancePath+"/cost_usd",schemaPath:"#/properties/cost_usd/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
validate298.errors = vErrors;
return false;
}
else {
errors = _errs3;
if(vErrors !== null){
if(_errs3){
vErrors.length = _errs3;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
}
}
}
else {
validate298.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate298.errors = vErrors;
return errors === 0;
}
validate298.evaluated = {"props":{"ads":true,"cost_usd":true},"dynamicProps":false,"dynamicItems":false};


function validate295(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate295.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.ref === undefined) && (missing0 = "ref"))) || ((data.applies_from === undefined) && (missing0 = "applies_from"))){
validate295.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("saved" !== data.type){
validate295.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "saved"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate295.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.ref !== undefined){
const _errs3 = errors;
if(!(validate296(data.ref, {instancePath:instancePath+"/ref",parentData:data,parentDataProperty:"ref",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate296.errors : vErrors.concat(validate296.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.applies_from !== undefined){
let data3 = data.applies_from;
const _errs4 = errors;
if(typeof data3 !== "string"){
validate295.errors = [{instancePath:instancePath+"/applies_from",schemaPath:"#/properties/applies_from/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
if(!((data3 === "now") || (data3 === "next_run"))){
validate295.errors = [{instancePath:instancePath+"/applies_from",schemaPath:"#/properties/applies_from/enum",keyword:"enum",params:{allowedValues: schema97.properties.applies_from.enum},message:"must be equal to one of the allowed values"}];
return false;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.recheck !== undefined){
const _errs6 = errors;
if(!(validate298(data.recheck, {instancePath:instancePath+"/recheck",parentData:data,parentDataProperty:"recheck",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate298.errors : vErrors.concat(validate298.errors);
errors = vErrors.length;
}
var valid0 = _errs6 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
else {
validate295.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate295.errors = vErrors;
return errors === 0;
}
validate295.evaluated = {"props":{"type":true,"v":true,"ref":true,"applies_from":true,"recheck":true},"dynamicProps":false,"dynamicItems":false};

const schema100 = {"type":"object","required":["type","v","ref","code","message","field"],"properties":{"type":{"const":"error"},"v":{"const":1},"ref":{"anyOf":[{"$ref":"#/$defs/MessageId"},{"type":"null"}]},"code":{"type":"string","enum":["invalid_payload","unknown_type","unsupported_version","not_found","run_in_progress","no_run","run_failed","key_rejected","cv_type","cv_unreadable","cv_no_text","cv_too_large","cv_parse_failed","chunk_incomplete","internal","guidance_empty","context_full","context_unreadable"]},"message":{"type":"string","minLength":1,"maxLength":300},"field":{"anyOf":[{"type":"string","maxLength":120},{"type":"null"}]}}};

function validate302(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate302.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.ref === undefined) && (missing0 = "ref"))) || ((data.code === undefined) && (missing0 = "code"))) || ((data.message === undefined) && (missing0 = "message"))) || ((data.field === undefined) && (missing0 = "field"))){
validate302.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("error" !== data.type){
validate302.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "error"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate302.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.ref !== undefined){
let data2 = data.ref;
const _errs3 = errors;
const _errs4 = errors;
let valid1 = false;
const _errs5 = errors;
if(!(validate296(data2, {instancePath:instancePath+"/ref",parentData:data,parentDataProperty:"ref",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate296.errors : vErrors.concat(validate296.errors);
errors = vErrors.length;
}
var _valid0 = _errs5 === errors;
valid1 = valid1 || _valid0;
const _errs6 = errors;
if(data2 !== null){
const err0 = {instancePath:instancePath+"/ref",schemaPath:"#/properties/ref/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
var _valid0 = _errs6 === errors;
valid1 = valid1 || _valid0;
if(!valid1){
const err1 = {instancePath:instancePath+"/ref",schemaPath:"#/properties/ref/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
validate302.errors = vErrors;
return false;
}
else {
errors = _errs4;
if(vErrors !== null){
if(_errs4){
vErrors.length = _errs4;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.code !== undefined){
let data3 = data.code;
const _errs8 = errors;
if(typeof data3 !== "string"){
validate302.errors = [{instancePath:instancePath+"/code",schemaPath:"#/properties/code/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
if(!((((((((((((((((((data3 === "invalid_payload") || (data3 === "unknown_type")) || (data3 === "unsupported_version")) || (data3 === "not_found")) || (data3 === "run_in_progress")) || (data3 === "no_run")) || (data3 === "run_failed")) || (data3 === "key_rejected")) || (data3 === "cv_type")) || (data3 === "cv_unreadable")) || (data3 === "cv_no_text")) || (data3 === "cv_too_large")) || (data3 === "cv_parse_failed")) || (data3 === "chunk_incomplete")) || (data3 === "internal")) || (data3 === "guidance_empty")) || (data3 === "context_full")) || (data3 === "context_unreadable"))){
validate302.errors = [{instancePath:instancePath+"/code",schemaPath:"#/properties/code/enum",keyword:"enum",params:{allowedValues: schema100.properties.code.enum},message:"must be equal to one of the allowed values"}];
return false;
}
var valid0 = _errs8 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.message !== undefined){
let data4 = data.message;
const _errs10 = errors;
if(errors === _errs10){
if(typeof data4 === "string"){
if(func1(data4) > 300){
validate302.errors = [{instancePath:instancePath+"/message",schemaPath:"#/properties/message/maxLength",keyword:"maxLength",params:{limit: 300},message:"must NOT have more than 300 characters"}];
return false;
}
else {
if(func1(data4) < 1){
validate302.errors = [{instancePath:instancePath+"/message",schemaPath:"#/properties/message/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"}];
return false;
}
}
}
else {
validate302.errors = [{instancePath:instancePath+"/message",schemaPath:"#/properties/message/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs10 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.field !== undefined){
let data5 = data.field;
const _errs12 = errors;
const _errs13 = errors;
let valid2 = false;
const _errs14 = errors;
if(errors === _errs14){
if(typeof data5 === "string"){
if(func1(data5) > 120){
const err2 = {instancePath:instancePath+"/field",schemaPath:"#/properties/field/anyOf/0/maxLength",keyword:"maxLength",params:{limit: 120},message:"must NOT have more than 120 characters"};
if(vErrors === null){
vErrors = [err2];
}
else {
vErrors.push(err2);
}
errors++;
}
}
else {
const err3 = {instancePath:instancePath+"/field",schemaPath:"#/properties/field/anyOf/0/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err3];
}
else {
vErrors.push(err3);
}
errors++;
}
}
var _valid1 = _errs14 === errors;
valid2 = valid2 || _valid1;
const _errs16 = errors;
if(data5 !== null){
const err4 = {instancePath:instancePath+"/field",schemaPath:"#/properties/field/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err4];
}
else {
vErrors.push(err4);
}
errors++;
}
var _valid1 = _errs16 === errors;
valid2 = valid2 || _valid1;
if(!valid2){
const err5 = {instancePath:instancePath+"/field",schemaPath:"#/properties/field/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err5];
}
else {
vErrors.push(err5);
}
errors++;
validate302.errors = vErrors;
return false;
}
else {
errors = _errs13;
if(vErrors !== null){
if(_errs13){
vErrors.length = _errs13;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs12 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
else {
validate302.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate302.errors = vErrors;
return errors === 0;
}
validate302.evaluated = {"props":{"type":true,"v":true,"ref":true,"code":true,"message":true,"field":true},"dynamicProps":false,"dynamicItems":false};

const schema101 = {"type":"object","required":["type","v","reason"],"properties":{"type":{"const":"unpaired"},"v":{"const":1},"reason":{"type":"string","enum":["refused","web","cli","revoked"]}}};

function validate304(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate304.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.reason === undefined) && (missing0 = "reason"))){
validate304.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("unpaired" !== data.type){
validate304.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "unpaired"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate304.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.reason !== undefined){
let data2 = data.reason;
const _errs3 = errors;
if(typeof data2 !== "string"){
validate304.errors = [{instancePath:instancePath+"/reason",schemaPath:"#/properties/reason/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
if(!((((data2 === "refused") || (data2 === "web")) || (data2 === "cli")) || (data2 === "revoked"))){
validate304.errors = [{instancePath:instancePath+"/reason",schemaPath:"#/properties/reason/enum",keyword:"enum",params:{allowedValues: schema101.properties.reason.enum},message:"must be equal to one of the allowed values"}];
return false;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
else {
validate304.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate304.errors = vErrors;
return errors === 0;
}
validate304.evaluated = {"props":{"type":true,"v":true,"reason":true},"dynamicProps":false,"dynamicItems":false};

const schema102 = {"type":"object","required":["type","v","id"],"properties":{"type":{"const":"run.start"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"}}};

function validate305(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate305.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.id === undefined) && (missing0 = "id"))){
validate305.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("run.start" !== data.type){
validate305.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "run.start"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate305.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.id !== undefined){
const _errs3 = errors;
if(!(validate296(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate296.errors : vErrors.concat(validate296.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
else {
validate305.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate305.errors = vErrors;
return errors === 0;
}
validate305.evaluated = {"props":{"type":true,"v":true,"id":true},"dynamicProps":false,"dynamicItems":false};

const schema103 = {"type":"object","required":["type","v","id","run_id"],"properties":{"type":{"const":"run.cancel"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"run_id":{"$ref":"#/$defs/RunId"}}};

function validate307(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate307.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.id === undefined) && (missing0 = "id"))) || ((data.run_id === undefined) && (missing0 = "run_id"))){
validate307.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("run.cancel" !== data.type){
validate307.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "run.cancel"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate307.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.id !== undefined){
const _errs3 = errors;
if(!(validate296(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate296.errors : vErrors.concat(validate296.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.run_id !== undefined){
const _errs4 = errors;
if(!(validate57(data.run_id, {instancePath:instancePath+"/run_id",parentData:data,parentDataProperty:"run_id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate57.errors : vErrors.concat(validate57.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
else {
validate307.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate307.errors = vErrors;
return errors === 0;
}
validate307.evaluated = {"props":{"type":true,"v":true,"id":true,"run_id":true},"dynamicProps":false,"dynamicItems":false};

const schema104 = {"type":"object","required":["type","v","id","ad_id","mark"],"properties":{"type":{"const":"ad.mark"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"ad_id":{"$ref":"#/$defs/AdId"},"mark":{"$ref":"#/$defs/Mark"}}};

function validate310(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate310.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.id === undefined) && (missing0 = "id"))) || ((data.ad_id === undefined) && (missing0 = "ad_id"))) || ((data.mark === undefined) && (missing0 = "mark"))){
validate310.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("ad.mark" !== data.type){
validate310.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "ad.mark"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate310.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.id !== undefined){
const _errs3 = errors;
if(!(validate296(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate296.errors : vErrors.concat(validate296.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.ad_id !== undefined){
const _errs4 = errors;
if(!(validate79(data.ad_id, {instancePath:instancePath+"/ad_id",parentData:data,parentDataProperty:"ad_id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate79.errors : vErrors.concat(validate79.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.mark !== undefined){
const _errs5 = errors;
if(!(validate105(data.mark, {instancePath:instancePath+"/mark",parentData:data,parentDataProperty:"mark",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate105.errors : vErrors.concat(validate105.errors);
errors = vErrors.length;
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
else {
validate310.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate310.errors = vErrors;
return errors === 0;
}
validate310.evaluated = {"props":{"type":true,"v":true,"id":true,"ad_id":true,"mark":true},"dynamicProps":false,"dynamicItems":false};

const schema105 = {"type":"object","required":["type","v","id","ad_id"],"properties":{"type":{"const":"ad.unmark"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"ad_id":{"$ref":"#/$defs/AdId"}}};

function validate314(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate314.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.id === undefined) && (missing0 = "id"))) || ((data.ad_id === undefined) && (missing0 = "ad_id"))){
validate314.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("ad.unmark" !== data.type){
validate314.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "ad.unmark"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate314.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.id !== undefined){
const _errs3 = errors;
if(!(validate296(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate296.errors : vErrors.concat(validate296.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.ad_id !== undefined){
const _errs4 = errors;
if(!(validate79(data.ad_id, {instancePath:instancePath+"/ad_id",parentData:data,parentDataProperty:"ad_id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate79.errors : vErrors.concat(validate79.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
else {
validate314.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate314.errors = vErrors;
return errors === 0;
}
validate314.evaluated = {"props":{"type":true,"v":true,"id":true,"ad_id":true},"dynamicProps":false,"dynamicItems":false};

const schema106 = {"type":"object","required":["type","v","id","ad_id","on"],"properties":{"type":{"const":"ad.show_anyway"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"ad_id":{"$ref":"#/$defs/AdId"},"on":{"type":"boolean"}}};

function validate317(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate317.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.id === undefined) && (missing0 = "id"))) || ((data.ad_id === undefined) && (missing0 = "ad_id"))) || ((data.on === undefined) && (missing0 = "on"))){
validate317.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("ad.show_anyway" !== data.type){
validate317.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "ad.show_anyway"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate317.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.id !== undefined){
const _errs3 = errors;
if(!(validate296(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate296.errors : vErrors.concat(validate296.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.ad_id !== undefined){
const _errs4 = errors;
if(!(validate79(data.ad_id, {instancePath:instancePath+"/ad_id",parentData:data,parentDataProperty:"ad_id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate79.errors : vErrors.concat(validate79.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.on !== undefined){
const _errs5 = errors;
if(typeof data.on !== "boolean"){
validate317.errors = [{instancePath:instancePath+"/on",schemaPath:"#/properties/on/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
else {
validate317.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate317.errors = vErrors;
return errors === 0;
}
validate317.evaluated = {"props":{"type":true,"v":true,"id":true,"ad_id":true,"on":true},"dynamicProps":false,"dynamicItems":false};

const schema107 = {"type":"object","required":["type","v","id","company","title","note"],"properties":{"type":{"const":"application.create"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"company":{"$ref":"#/$defs/Company","type":"string","pattern":"[^\\u0000-\\u0020\\u007f-\\u00a0\\u00ad\\u034f\\u061c\\u115f-\\u1160\\u1680\\u17b4-\\u17b5\\u180b-\\u180f\\u2000-\\u200f\\u2028-\\u202f\\u205f-\\u206f\\u2800\\u3000\\u3164\\ufe00-\\ufe0f\\ufeff\\uffa0\\ufff0-\\ufff8\ud82f\udca0-\ud82f\udca3\ud834\udd73-\ud834\udd7a\udb40\udc00-\udb43\udfff]"},"title":{"type":"string","maxLength":200},"note":{"$ref":"#/$defs/Note"}}};
const pattern17 = new RegExp("[^\\u0000-\\u0020\\u007f-\\u00a0\\u00ad\\u034f\\u061c\\u115f-\\u1160\\u1680\\u17b4-\\u17b5\\u180b-\\u180f\\u2000-\\u200f\\u2028-\\u202f\\u205f-\\u206f\\u2800\\u3000\\u3164\\ufe00-\\ufe0f\\ufeff\\uffa0\\ufff0-\\ufff8\ud82f\udca0-\ud82f\udca3\ud834\udd73-\ud834\udd7a\udb40\udc00-\udb43\udfff]", "u");

function validate320(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate320.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.id === undefined) && (missing0 = "id"))) || ((data.company === undefined) && (missing0 = "company"))) || ((data.title === undefined) && (missing0 = "title"))) || ((data.note === undefined) && (missing0 = "note"))){
validate320.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("application.create" !== data.type){
validate320.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "application.create"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate320.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.id !== undefined){
const _errs3 = errors;
if(!(validate296(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate296.errors : vErrors.concat(validate296.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.company !== undefined){
let data3 = data.company;
const _errs4 = errors;
if(!(validate81(data3, {instancePath:instancePath+"/company",parentData:data,parentDataProperty:"company",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate81.errors : vErrors.concat(validate81.errors);
errors = vErrors.length;
}
if(errors === _errs4){
if(typeof data3 === "string"){
if(!pattern17.test(data3)){
validate320.errors = [{instancePath:instancePath+"/company",schemaPath:"#/properties/company/pattern",keyword:"pattern",params:{pattern: "[^\\u0000-\\u0020\\u007f-\\u00a0\\u00ad\\u034f\\u061c\\u115f-\\u1160\\u1680\\u17b4-\\u17b5\\u180b-\\u180f\\u2000-\\u200f\\u2028-\\u202f\\u205f-\\u206f\\u2800\\u3000\\u3164\\ufe00-\\ufe0f\\ufeff\\uffa0\\ufff0-\\ufff8\ud82f\udca0-\ud82f\udca3\ud834\udd73-\ud834\udd7a\udb40\udc00-\udb43\udfff]"},message:"must match pattern \""+"[^\\u0000-\\u0020\\u007f-\\u00a0\\u00ad\\u034f\\u061c\\u115f-\\u1160\\u1680\\u17b4-\\u17b5\\u180b-\\u180f\\u2000-\\u200f\\u2028-\\u202f\\u205f-\\u206f\\u2800\\u3000\\u3164\\ufe00-\\ufe0f\\ufeff\\uffa0\\ufff0-\\ufff8\ud82f\udca0-\ud82f\udca3\ud834\udd73-\ud834\udd7a\udb40\udc00-\udb43\udfff]"+"\""}];
return false;
}
}
else {
validate320.errors = [{instancePath:instancePath+"/company",schemaPath:"#/properties/company/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.title !== undefined){
let data4 = data.title;
const _errs6 = errors;
if(errors === _errs6){
if(typeof data4 === "string"){
if(func1(data4) > 200){
validate320.errors = [{instancePath:instancePath+"/title",schemaPath:"#/properties/title/maxLength",keyword:"maxLength",params:{limit: 200},message:"must NOT have more than 200 characters"}];
return false;
}
}
else {
validate320.errors = [{instancePath:instancePath+"/title",schemaPath:"#/properties/title/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs6 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.note !== undefined){
const _errs8 = errors;
if(!(validate156(data.note, {instancePath:instancePath+"/note",parentData:data,parentDataProperty:"note",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate156.errors : vErrors.concat(validate156.errors);
errors = vErrors.length;
}
var valid0 = _errs8 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
else {
validate320.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate320.errors = vErrors;
return errors === 0;
}
validate320.evaluated = {"props":{"type":true,"v":true,"id":true,"company":true,"title":true,"note":true},"dynamicProps":false,"dynamicItems":false};

const schema108 = {"type":"object","required":["type","v","id","application_id"],"properties":{"type":{"const":"application.update"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"application_id":{"$ref":"#/$defs/ApplicationId"},"status":{"$ref":"#/$defs/ApplicationStatus"},"note":{"$ref":"#/$defs/Note"}}};

function validate324(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate324.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.id === undefined) && (missing0 = "id"))) || ((data.application_id === undefined) && (missing0 = "application_id"))){
validate324.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("application.update" !== data.type){
validate324.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "application.update"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate324.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.id !== undefined){
const _errs3 = errors;
if(!(validate296(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate296.errors : vErrors.concat(validate296.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.application_id !== undefined){
const _errs4 = errors;
if(!(validate145(data.application_id, {instancePath:instancePath+"/application_id",parentData:data,parentDataProperty:"application_id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate145.errors : vErrors.concat(validate145.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.status !== undefined){
const _errs5 = errors;
if(!(validate154(data.status, {instancePath:instancePath+"/status",parentData:data,parentDataProperty:"status",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate154.errors : vErrors.concat(validate154.errors);
errors = vErrors.length;
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.note !== undefined){
const _errs6 = errors;
if(!(validate156(data.note, {instancePath:instancePath+"/note",parentData:data,parentDataProperty:"note",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate156.errors : vErrors.concat(validate156.errors);
errors = vErrors.length;
}
var valid0 = _errs6 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
else {
validate324.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate324.errors = vErrors;
return errors === 0;
}
validate324.evaluated = {"props":{"type":true,"v":true,"id":true,"application_id":true,"status":true,"note":true},"dynamicProps":false,"dynamicItems":false};

const schema109 = {"type":"object","required":["type","v","id","application_id","removed"],"properties":{"type":{"const":"application.remove"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"application_id":{"$ref":"#/$defs/ApplicationId"},"removed":{"type":"boolean"}}};

function validate329(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate329.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.id === undefined) && (missing0 = "id"))) || ((data.application_id === undefined) && (missing0 = "application_id"))) || ((data.removed === undefined) && (missing0 = "removed"))){
validate329.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("application.remove" !== data.type){
validate329.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "application.remove"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate329.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.id !== undefined){
const _errs3 = errors;
if(!(validate296(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate296.errors : vErrors.concat(validate296.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.application_id !== undefined){
const _errs4 = errors;
if(!(validate145(data.application_id, {instancePath:instancePath+"/application_id",parentData:data,parentDataProperty:"application_id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate145.errors : vErrors.concat(validate145.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.removed !== undefined){
const _errs5 = errors;
if(typeof data.removed !== "boolean"){
validate329.errors = [{instancePath:instancePath+"/removed",schemaPath:"#/properties/removed/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
else {
validate329.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate329.errors = vErrors;
return errors === 0;
}
validate329.evaluated = {"props":{"type":true,"v":true,"id":true,"application_id":true,"removed":true},"dynamicProps":false,"dynamicItems":false};

const schema110 = {"type":"object","required":["type","v","id","patch"],"properties":{"type":{"const":"config.update"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"patch":{"$ref":"#/$defs/ConfigPatch"}}};
const schema111 = {"type":"object","properties":{"targets":{"$ref":"#/$defs/TargetsPatch"},"sources":{"$ref":"#/$defs/SourcesPatch"},"schedule":{"$ref":"#/$defs/SchedulePatch"},"notify":{"$ref":"#/$defs/NotifyPatch"},"model":{"$ref":"#/$defs/ModelConfigPatch"},"language":{"$ref":"#/$defs/Language"}}};
const schema112 = {"type":"object","properties":{"subfields":{"$ref":"#/$defs/RoleIds"},"levels":{"$ref":"#/$defs/Levels"},"years":{"$ref":"#/$defs/YearBands"},"hide_masters":{"type":"boolean"},"places":{"$ref":"#/$defs/Places"},"relocate":{"type":"boolean"}}};

function validate335(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate335.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
if(data.subfields !== undefined){
const _errs1 = errors;
if(!(validate171(data.subfields, {instancePath:instancePath+"/subfields",parentData:data,parentDataProperty:"subfields",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate171.errors : vErrors.concat(validate171.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.levels !== undefined){
const _errs2 = errors;
if(!(validate174(data.levels, {instancePath:instancePath+"/levels",parentData:data,parentDataProperty:"levels",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate174.errors : vErrors.concat(validate174.errors);
errors = vErrors.length;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.years !== undefined){
const _errs3 = errors;
if(!(validate178(data.years, {instancePath:instancePath+"/years",parentData:data,parentDataProperty:"years",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate178.errors : vErrors.concat(validate178.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.hide_masters !== undefined){
const _errs4 = errors;
if(typeof data.hide_masters !== "boolean"){
validate335.errors = [{instancePath:instancePath+"/hide_masters",schemaPath:"#/properties/hide_masters/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.places !== undefined){
const _errs6 = errors;
if(!(validate182(data.places, {instancePath:instancePath+"/places",parentData:data,parentDataProperty:"places",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate182.errors : vErrors.concat(validate182.errors);
errors = vErrors.length;
}
var valid0 = _errs6 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.relocate !== undefined){
const _errs7 = errors;
if(typeof data.relocate !== "boolean"){
validate335.errors = [{instancePath:instancePath+"/relocate",schemaPath:"#/properties/relocate/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs7 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
else {
validate335.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate335.errors = vErrors;
return errors === 0;
}
validate335.evaluated = {"props":{"subfields":true,"levels":true,"years":true,"hide_masters":true,"places":true,"relocate":true},"dynamicProps":false,"dynamicItems":false};

const schema113 = {"type":"object","properties":{"nav":{"type":"boolean"},"employers":{"type":"boolean"},"finn":{"type":"boolean"},"linkedin":{"type":"boolean"}}};

function validate341(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate341.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
if(data.nav !== undefined){
const _errs1 = errors;
if(typeof data.nav !== "boolean"){
validate341.errors = [{instancePath:instancePath+"/nav",schemaPath:"#/properties/nav/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.employers !== undefined){
const _errs3 = errors;
if(typeof data.employers !== "boolean"){
validate341.errors = [{instancePath:instancePath+"/employers",schemaPath:"#/properties/employers/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.finn !== undefined){
const _errs5 = errors;
if(typeof data.finn !== "boolean"){
validate341.errors = [{instancePath:instancePath+"/finn",schemaPath:"#/properties/finn/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.linkedin !== undefined){
const _errs7 = errors;
if(typeof data.linkedin !== "boolean"){
validate341.errors = [{instancePath:instancePath+"/linkedin",schemaPath:"#/properties/linkedin/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs7 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
else {
validate341.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate341.errors = vErrors;
return errors === 0;
}
validate341.evaluated = {"props":{"nav":true,"employers":true,"finn":true,"linkedin":true},"dynamicProps":false,"dynamicItems":false};

const schema114 = {"type":"object","properties":{"times":{"$ref":"#/$defs/ClockTimes"}}};

function validate343(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate343.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
if(data.times !== undefined){
if(!(validate188(data.times, {instancePath:instancePath+"/times",parentData:data,parentDataProperty:"times",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate188.errors : vErrors.concat(validate188.errors);
errors = vErrors.length;
}
}
}
else {
validate343.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate343.errors = vErrors;
return errors === 0;
}
validate343.evaluated = {"props":{"times":true},"dynamicProps":false,"dynamicItems":false};

const schema115 = {"type":"object","properties":{"website":{"type":"boolean"},"email":{"$ref":"#/$defs/EmailNotifyPatch"}}};
const schema116 = {"type":"object","properties":{"on":{"type":"boolean"},"address":{"anyOf":[{"$ref":"#/$defs/Email"},{"type":"null"}]}}};

function validate347(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate347.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
if(data.on !== undefined){
const _errs1 = errors;
if(typeof data.on !== "boolean"){
validate347.errors = [{instancePath:instancePath+"/on",schemaPath:"#/properties/on/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.address !== undefined){
let data1 = data.address;
const _errs3 = errors;
const _errs4 = errors;
let valid1 = false;
const _errs5 = errors;
if(!(validate195(data1, {instancePath:instancePath+"/address",parentData:data,parentDataProperty:"address",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate195.errors : vErrors.concat(validate195.errors);
errors = vErrors.length;
}
var _valid0 = _errs5 === errors;
valid1 = valid1 || _valid0;
const _errs6 = errors;
if(data1 !== null){
const err0 = {instancePath:instancePath+"/address",schemaPath:"#/properties/address/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
var _valid0 = _errs6 === errors;
valid1 = valid1 || _valid0;
if(!valid1){
const err1 = {instancePath:instancePath+"/address",schemaPath:"#/properties/address/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
validate347.errors = vErrors;
return false;
}
else {
errors = _errs4;
if(vErrors !== null){
if(_errs4){
vErrors.length = _errs4;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
}
}
else {
validate347.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate347.errors = vErrors;
return errors === 0;
}
validate347.evaluated = {"props":{"on":true,"address":true},"dynamicProps":false,"dynamicItems":false};


function validate346(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate346.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
if(data.website !== undefined){
const _errs1 = errors;
if(typeof data.website !== "boolean"){
validate346.errors = [{instancePath:instancePath+"/website",schemaPath:"#/properties/website/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.email !== undefined){
const _errs3 = errors;
if(!(validate347(data.email, {instancePath:instancePath+"/email",parentData:data,parentDataProperty:"email",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate347.errors : vErrors.concat(validate347.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
}
}
else {
validate346.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate346.errors = vErrors;
return errors === 0;
}
validate346.evaluated = {"props":{"website":true,"email":true},"dynamicProps":false,"dynamicItems":false};

const schema117 = {"type":"object","properties":{"price":{"$ref":"#/$defs/PricePatch"},"monthly_limit_usd":{"anyOf":[{"$ref":"#/$defs/Money"},{"type":"null"}]}}};
const schema118 = {"type":"object","properties":{"input_per_mtok":{"$ref":"#/$defs/Money"},"output_per_mtok":{"$ref":"#/$defs/Money"}}};

function validate352(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate352.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
if(data.input_per_mtok !== undefined){
const _errs1 = errors;
if(!(validate63(data.input_per_mtok, {instancePath:instancePath+"/input_per_mtok",parentData:data,parentDataProperty:"input_per_mtok",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate63.errors : vErrors.concat(validate63.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.output_per_mtok !== undefined){
const _errs2 = errors;
if(!(validate63(data.output_per_mtok, {instancePath:instancePath+"/output_per_mtok",parentData:data,parentDataProperty:"output_per_mtok",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate63.errors : vErrors.concat(validate63.errors);
errors = vErrors.length;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
}
}
else {
validate352.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate352.errors = vErrors;
return errors === 0;
}
validate352.evaluated = {"props":{"input_per_mtok":true,"output_per_mtok":true},"dynamicProps":false,"dynamicItems":false};


function validate351(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate351.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
if(data.price !== undefined){
const _errs1 = errors;
if(!(validate352(data.price, {instancePath:instancePath+"/price",parentData:data,parentDataProperty:"price",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate352.errors : vErrors.concat(validate352.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.monthly_limit_usd !== undefined){
let data1 = data.monthly_limit_usd;
const _errs2 = errors;
const _errs3 = errors;
let valid1 = false;
const _errs4 = errors;
if(!(validate63(data1, {instancePath:instancePath+"/monthly_limit_usd",parentData:data,parentDataProperty:"monthly_limit_usd",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate63.errors : vErrors.concat(validate63.errors);
errors = vErrors.length;
}
var _valid0 = _errs4 === errors;
valid1 = valid1 || _valid0;
const _errs5 = errors;
if(data1 !== null){
const err0 = {instancePath:instancePath+"/monthly_limit_usd",schemaPath:"#/properties/monthly_limit_usd/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
var _valid0 = _errs5 === errors;
valid1 = valid1 || _valid0;
if(!valid1){
const err1 = {instancePath:instancePath+"/monthly_limit_usd",schemaPath:"#/properties/monthly_limit_usd/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
validate351.errors = vErrors;
return false;
}
else {
errors = _errs3;
if(vErrors !== null){
if(_errs3){
vErrors.length = _errs3;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
}
}
else {
validate351.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate351.errors = vErrors;
return errors === 0;
}
validate351.evaluated = {"props":{"price":true,"monthly_limit_usd":true},"dynamicProps":false,"dynamicItems":false};


function validate334(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate334.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
if(data.targets !== undefined){
const _errs1 = errors;
if(!(validate335(data.targets, {instancePath:instancePath+"/targets",parentData:data,parentDataProperty:"targets",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate335.errors : vErrors.concat(validate335.errors);
errors = vErrors.length;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.sources !== undefined){
const _errs2 = errors;
if(!(validate341(data.sources, {instancePath:instancePath+"/sources",parentData:data,parentDataProperty:"sources",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate341.errors : vErrors.concat(validate341.errors);
errors = vErrors.length;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.schedule !== undefined){
const _errs3 = errors;
if(!(validate343(data.schedule, {instancePath:instancePath+"/schedule",parentData:data,parentDataProperty:"schedule",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate343.errors : vErrors.concat(validate343.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.notify !== undefined){
const _errs4 = errors;
if(!(validate346(data.notify, {instancePath:instancePath+"/notify",parentData:data,parentDataProperty:"notify",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate346.errors : vErrors.concat(validate346.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.model !== undefined){
const _errs5 = errors;
if(!(validate351(data.model, {instancePath:instancePath+"/model",parentData:data,parentDataProperty:"model",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate351.errors : vErrors.concat(validate351.errors);
errors = vErrors.length;
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.language !== undefined){
const _errs6 = errors;
if(!(validate210(data.language, {instancePath:instancePath+"/language",parentData:data,parentDataProperty:"language",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate210.errors : vErrors.concat(validate210.errors);
errors = vErrors.length;
}
var valid0 = _errs6 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
else {
validate334.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate334.errors = vErrors;
return errors === 0;
}
validate334.evaluated = {"props":{"targets":true,"sources":true,"schedule":true,"notify":true,"model":true,"language":true},"dynamicProps":false,"dynamicItems":false};


function validate332(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate332.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.id === undefined) && (missing0 = "id"))) || ((data.patch === undefined) && (missing0 = "patch"))){
validate332.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("config.update" !== data.type){
validate332.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "config.update"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate332.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.id !== undefined){
const _errs3 = errors;
if(!(validate296(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate296.errors : vErrors.concat(validate296.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.patch !== undefined){
const _errs4 = errors;
if(!(validate334(data.patch, {instancePath:instancePath+"/patch",parentData:data,parentDataProperty:"patch",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate334.errors : vErrors.concat(validate334.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
else {
validate332.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate332.errors = vErrors;
return errors === 0;
}
validate332.evaluated = {"props":{"type":true,"v":true,"id":true,"patch":true},"dynamicProps":false,"dynamicItems":false};

const schema119 = {"type":"object","required":["type","v","id","provider","model"],"properties":{"type":{"const":"model.select"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"provider":{"$ref":"#/$defs/ProviderId"},"model":{"$ref":"#/$defs/ModelId"}}};

function validate360(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate360.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.id === undefined) && (missing0 = "id"))) || ((data.provider === undefined) && (missing0 = "provider"))) || ((data.model === undefined) && (missing0 = "model"))){
validate360.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("model.select" !== data.type){
validate360.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "model.select"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate360.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.id !== undefined){
const _errs3 = errors;
if(!(validate296(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate296.errors : vErrors.concat(validate296.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.provider !== undefined){
const _errs4 = errors;
if(!(validate200(data.provider, {instancePath:instancePath+"/provider",parentData:data,parentDataProperty:"provider",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate200.errors : vErrors.concat(validate200.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.model !== undefined){
const _errs5 = errors;
if(!(validate202(data.model, {instancePath:instancePath+"/model",parentData:data,parentDataProperty:"model",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate202.errors : vErrors.concat(validate202.errors);
errors = vErrors.length;
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
else {
validate360.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate360.errors = vErrors;
return errors === 0;
}
validate360.evaluated = {"props":{"type":true,"v":true,"id":true,"provider":true,"model":true},"dynamicProps":false,"dynamicItems":false};

const schema120 = {"type":"object","required":["type","v","id","provider","key"],"properties":{"type":{"const":"key.replace"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"provider":{"$ref":"#/$defs/ProviderId"},"key":{"type":"string","pattern":"^[\\x21-\\x7e]{12,512}$","not":{"pattern":"[^\\x21-\\x7e]"}}}};
const pattern18 = new RegExp("[^\\x21-\\x7e]", "u");
const pattern19 = new RegExp("^[\\x21-\\x7e]{12,512}$", "u");

function validate364(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate364.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.id === undefined) && (missing0 = "id"))) || ((data.provider === undefined) && (missing0 = "provider"))) || ((data.key === undefined) && (missing0 = "key"))){
validate364.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("key.replace" !== data.type){
validate364.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "key.replace"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate364.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.id !== undefined){
const _errs3 = errors;
if(!(validate296(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate296.errors : vErrors.concat(validate296.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.provider !== undefined){
const _errs4 = errors;
if(!(validate200(data.provider, {instancePath:instancePath+"/provider",parentData:data,parentDataProperty:"provider",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate200.errors : vErrors.concat(validate200.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.key !== undefined){
let data4 = data.key;
const _errs5 = errors;
const _errs7 = errors;
const _errs8 = errors;
if(typeof data4 === "string"){
if(!pattern18.test(data4)){
const err0 = {};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
}
var valid1 = _errs8 === errors;
if(valid1){
validate364.errors = [{instancePath:instancePath+"/key",schemaPath:"#/properties/key/not",keyword:"not",params:{},message:"must NOT be valid"}];
return false;
}
else {
errors = _errs7;
if(vErrors !== null){
if(_errs7){
vErrors.length = _errs7;
}
else {
vErrors = null;
}
}
}
if(errors === _errs5){
if(typeof data4 === "string"){
if(!pattern19.test(data4)){
validate364.errors = [{instancePath:instancePath+"/key",schemaPath:"#/properties/key/pattern",keyword:"pattern",params:{pattern: "^[\\x21-\\x7e]{12,512}$"},message:"must match pattern \""+"^[\\x21-\\x7e]{12,512}$"+"\""}];
return false;
}
}
else {
validate364.errors = [{instancePath:instancePath+"/key",schemaPath:"#/properties/key/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
else {
validate364.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate364.errors = vErrors;
return errors === 0;
}
validate364.evaluated = {"props":{"type":true,"v":true,"id":true,"provider":true,"key":true},"dynamicProps":false,"dynamicItems":false};

const schema121 = {"type":"object","required":["type","v","id","name","size_bytes","content_b64"],"properties":{"type":{"const":"cv.upload"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"name":{"$ref":"#/$defs/FileName"},"size_bytes":{"type":"integer","minimum":1,"maximum":5242880},"content_b64":{"$ref":"#/$defs/Base64Data","type":"string","maxLength":6990508}}};
const schema122 = {"type":"string","minLength":1,"maxLength":120,"pattern":"\\.([pP][dD][fF]|[tT][xX][tT]|[mM][dD])$","not":{"pattern":"[\\u0000-\\u001f\\u007f-\\u009f\\u00ad\\u034f\\u061c\\u115f-\\u1160\\u17b4-\\u17b5\\u180b-\\u180f\\u200b\\u200e-\\u200f\\u2028-\\u202e\\u2060-\\u206f\\u3164\\ufe00-\\ufe0f\\ufeff\\uffa0\\ufff0-\\ufff8\ud82f\udca0-\ud82f\udca3\ud834\udd73-\ud834\udd7a\udb40\udc00-\udb43\udfff/\\\\]"}};
const pattern20 = new RegExp("[\\u0000-\\u001f\\u007f-\\u009f\\u00ad\\u034f\\u061c\\u115f-\\u1160\\u17b4-\\u17b5\\u180b-\\u180f\\u200b\\u200e-\\u200f\\u2028-\\u202e\\u2060-\\u206f\\u3164\\ufe00-\\ufe0f\\ufeff\\uffa0\\ufff0-\\ufff8\ud82f\udca0-\ud82f\udca3\ud834\udd73-\ud834\udd7a\udb40\udc00-\udb43\udfff/\\\\]", "u");
const pattern21 = new RegExp("\\.([pP][dD][fF]|[tT][xX][tT]|[mM][dD])$", "u");

function validate369(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate369.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
const _errs1 = errors;
const _errs2 = errors;
if(typeof data === "string"){
if(!pattern20.test(data)){
const err0 = {};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
}
var valid0 = _errs2 === errors;
if(valid0){
validate369.errors = [{instancePath,schemaPath:"#/not",keyword:"not",params:{},message:"must NOT be valid"}];
return false;
}
else {
errors = _errs1;
if(vErrors !== null){
if(_errs1){
vErrors.length = _errs1;
}
else {
vErrors = null;
}
}
}
if(errors === 0){
if(typeof data === "string"){
if(func1(data) > 120){
validate369.errors = [{instancePath,schemaPath:"#/maxLength",keyword:"maxLength",params:{limit: 120},message:"must NOT have more than 120 characters"}];
return false;
}
else {
if(func1(data) < 1){
validate369.errors = [{instancePath,schemaPath:"#/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"}];
return false;
}
else {
if(!pattern21.test(data)){
validate369.errors = [{instancePath,schemaPath:"#/pattern",keyword:"pattern",params:{pattern: "\\.([pP][dD][fF]|[tT][xX][tT]|[mM][dD])$"},message:"must match pattern \""+"\\.([pP][dD][fF]|[tT][xX][tT]|[mM][dD])$"+"\""}];
return false;
}
}
}
}
else {
validate369.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
validate369.errors = vErrors;
return errors === 0;
}
validate369.evaluated = {"dynamicProps":false,"dynamicItems":false};

const schema123 = {"type":"string","pattern":"^[A-Za-z0-9+/]*={0,2}$","not":{"pattern":"[^A-Za-z0-9+/=]"}};
const pattern22 = new RegExp("[^A-Za-z0-9+/=]", "u");
const pattern23 = new RegExp("^[A-Za-z0-9+/]*={0,2}$", "u");

function validate371(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate371.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
const _errs1 = errors;
const _errs2 = errors;
if(typeof data === "string"){
if(!pattern22.test(data)){
const err0 = {};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
}
var valid0 = _errs2 === errors;
if(valid0){
validate371.errors = [{instancePath,schemaPath:"#/not",keyword:"not",params:{},message:"must NOT be valid"}];
return false;
}
else {
errors = _errs1;
if(vErrors !== null){
if(_errs1){
vErrors.length = _errs1;
}
else {
vErrors = null;
}
}
}
if(errors === 0){
if(typeof data === "string"){
if(!pattern23.test(data)){
validate371.errors = [{instancePath,schemaPath:"#/pattern",keyword:"pattern",params:{pattern: "^[A-Za-z0-9+/]*={0,2}$"},message:"must match pattern \""+"^[A-Za-z0-9+/]*={0,2}$"+"\""}];
return false;
}
}
else {
validate371.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
validate371.errors = vErrors;
return errors === 0;
}
validate371.evaluated = {"dynamicProps":false,"dynamicItems":false};


function validate367(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate367.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.id === undefined) && (missing0 = "id"))) || ((data.name === undefined) && (missing0 = "name"))) || ((data.size_bytes === undefined) && (missing0 = "size_bytes"))) || ((data.content_b64 === undefined) && (missing0 = "content_b64"))){
validate367.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("cv.upload" !== data.type){
validate367.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "cv.upload"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate367.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.id !== undefined){
const _errs3 = errors;
if(!(validate296(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate296.errors : vErrors.concat(validate296.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.name !== undefined){
const _errs4 = errors;
if(!(validate369(data.name, {instancePath:instancePath+"/name",parentData:data,parentDataProperty:"name",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate369.errors : vErrors.concat(validate369.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.size_bytes !== undefined){
let data4 = data.size_bytes;
const _errs5 = errors;
if(!(((typeof data4 == "number") && (!(data4 % 1) && !isNaN(data4))) && (isFinite(data4)))){
validate367.errors = [{instancePath:instancePath+"/size_bytes",schemaPath:"#/properties/size_bytes/type",keyword:"type",params:{type: "integer"},message:"must be integer"}];
return false;
}
if(errors === _errs5){
if((typeof data4 == "number") && (isFinite(data4))){
if(data4 > 5242880 || isNaN(data4)){
validate367.errors = [{instancePath:instancePath+"/size_bytes",schemaPath:"#/properties/size_bytes/maximum",keyword:"maximum",params:{comparison: "<=", limit: 5242880},message:"must be <= 5242880"}];
return false;
}
else {
if(data4 < 1 || isNaN(data4)){
validate367.errors = [{instancePath:instancePath+"/size_bytes",schemaPath:"#/properties/size_bytes/minimum",keyword:"minimum",params:{comparison: ">=", limit: 1},message:"must be >= 1"}];
return false;
}
}
}
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.content_b64 !== undefined){
let data5 = data.content_b64;
const _errs7 = errors;
if(!(validate371(data5, {instancePath:instancePath+"/content_b64",parentData:data,parentDataProperty:"content_b64",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate371.errors : vErrors.concat(validate371.errors);
errors = vErrors.length;
}
if(errors === _errs7){
if(typeof data5 === "string"){
if(func1(data5) > 6990508){
validate367.errors = [{instancePath:instancePath+"/content_b64",schemaPath:"#/properties/content_b64/maxLength",keyword:"maxLength",params:{limit: 6990508},message:"must NOT have more than 6990508 characters"}];
return false;
}
}
else {
validate367.errors = [{instancePath:instancePath+"/content_b64",schemaPath:"#/properties/content_b64/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs7 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
else {
validate367.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate367.errors = vErrors;
return errors === 0;
}
validate367.evaluated = {"props":{"type":true,"v":true,"id":true,"name":true,"size_bytes":true,"content_b64":true},"dynamicProps":false,"dynamicItems":false};

const schema124 = {"type":"object","required":["type","v","id"],"properties":{"type":{"const":"profile.update"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"summary":{"$ref":"#/$defs/ShortText"},"education":{"$ref":"#/$defs/ShortText"},"languages":{"$ref":"#/$defs/Terms"},"stack":{"$ref":"#/$defs/Terms"},"projects":{"type":"array","items":{"$ref":"#/$defs/ProjectEdit"},"maxItems":20}}};
const schema125 = {"type":"object","required":["id","name"],"properties":{"id":{"anyOf":[{"type":"string","minLength":1,"maxLength":40},{"type":"null"}]},"name":{"type":"string","minLength":1,"maxLength":120}}};

function validate379(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate379.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((data.id === undefined) && (missing0 = "id")) || ((data.name === undefined) && (missing0 = "name"))){
validate379.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.id !== undefined){
let data0 = data.id;
const _errs1 = errors;
const _errs2 = errors;
let valid1 = false;
const _errs3 = errors;
if(errors === _errs3){
if(typeof data0 === "string"){
if(func1(data0) > 40){
const err0 = {instancePath:instancePath+"/id",schemaPath:"#/properties/id/anyOf/0/maxLength",keyword:"maxLength",params:{limit: 40},message:"must NOT have more than 40 characters"};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
else {
if(func1(data0) < 1){
const err1 = {instancePath:instancePath+"/id",schemaPath:"#/properties/id/anyOf/0/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
}
}
}
else {
const err2 = {instancePath:instancePath+"/id",schemaPath:"#/properties/id/anyOf/0/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err2];
}
else {
vErrors.push(err2);
}
errors++;
}
}
var _valid0 = _errs3 === errors;
valid1 = valid1 || _valid0;
const _errs5 = errors;
if(data0 !== null){
const err3 = {instancePath:instancePath+"/id",schemaPath:"#/properties/id/anyOf/1/type",keyword:"type",params:{type: "null"},message:"must be null"};
if(vErrors === null){
vErrors = [err3];
}
else {
vErrors.push(err3);
}
errors++;
}
var _valid0 = _errs5 === errors;
valid1 = valid1 || _valid0;
if(!valid1){
const err4 = {instancePath:instancePath+"/id",schemaPath:"#/properties/id/anyOf",keyword:"anyOf",params:{},message:"must match a schema in anyOf"};
if(vErrors === null){
vErrors = [err4];
}
else {
vErrors.push(err4);
}
errors++;
validate379.errors = vErrors;
return false;
}
else {
errors = _errs2;
if(vErrors !== null){
if(_errs2){
vErrors.length = _errs2;
}
else {
vErrors = null;
}
}
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.name !== undefined){
let data1 = data.name;
const _errs7 = errors;
if(errors === _errs7){
if(typeof data1 === "string"){
if(func1(data1) > 120){
validate379.errors = [{instancePath:instancePath+"/name",schemaPath:"#/properties/name/maxLength",keyword:"maxLength",params:{limit: 120},message:"must NOT have more than 120 characters"}];
return false;
}
else {
if(func1(data1) < 1){
validate379.errors = [{instancePath:instancePath+"/name",schemaPath:"#/properties/name/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"}];
return false;
}
}
}
else {
validate379.errors = [{instancePath:instancePath+"/name",schemaPath:"#/properties/name/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs7 === errors;
}
else {
var valid0 = true;
}
}
}
}
else {
validate379.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate379.errors = vErrors;
return errors === 0;
}
validate379.evaluated = {"props":{"id":true,"name":true},"dynamicProps":false,"dynamicItems":false};


function validate373(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate373.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.id === undefined) && (missing0 = "id"))){
validate373.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("profile.update" !== data.type){
validate373.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "profile.update"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate373.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.id !== undefined){
const _errs3 = errors;
if(!(validate296(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate296.errors : vErrors.concat(validate296.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.summary !== undefined){
const _errs4 = errors;
if(!(validate98(data.summary, {instancePath:instancePath+"/summary",parentData:data,parentDataProperty:"summary",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate98.errors : vErrors.concat(validate98.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.education !== undefined){
const _errs5 = errors;
if(!(validate98(data.education, {instancePath:instancePath+"/education",parentData:data,parentDataProperty:"education",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate98.errors : vErrors.concat(validate98.errors);
errors = vErrors.length;
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.languages !== undefined){
const _errs6 = errors;
if(!(validate216(data.languages, {instancePath:instancePath+"/languages",parentData:data,parentDataProperty:"languages",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate216.errors : vErrors.concat(validate216.errors);
errors = vErrors.length;
}
var valid0 = _errs6 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.stack !== undefined){
const _errs7 = errors;
if(!(validate216(data.stack, {instancePath:instancePath+"/stack",parentData:data,parentDataProperty:"stack",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate216.errors : vErrors.concat(validate216.errors);
errors = vErrors.length;
}
var valid0 = _errs7 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.projects !== undefined){
let data7 = data.projects;
const _errs8 = errors;
if(errors === _errs8){
if(Array.isArray(data7)){
if(data7.length > 20){
validate373.errors = [{instancePath:instancePath+"/projects",schemaPath:"#/properties/projects/maxItems",keyword:"maxItems",params:{limit: 20},message:"must NOT have more than 20 items"}];
return false;
}
else {
var valid1 = true;
const len0 = data7.length;
for(let i0=0; i0<len0; i0++){
const _errs10 = errors;
if(!(validate379(data7[i0], {instancePath:instancePath+"/projects/" + i0,parentData:data7,parentDataProperty:i0,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate379.errors : vErrors.concat(validate379.errors);
errors = vErrors.length;
}
var valid1 = _errs10 === errors;
if(!valid1){
break;
}
}
}
}
else {
validate373.errors = [{instancePath:instancePath+"/projects",schemaPath:"#/properties/projects/type",keyword:"type",params:{type: "array"},message:"must be array"}];
return false;
}
}
var valid0 = _errs8 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
}
}
else {
validate373.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate373.errors = vErrors;
return errors === 0;
}
validate373.evaluated = {"props":{"type":true,"v":true,"id":true,"summary":true,"education":true,"languages":true,"stack":true,"projects":true},"dynamicProps":false,"dynamicItems":false};

const schema126 = {"type":"object","required":["type","v","id","text"],"properties":{"type":{"const":"guidance.update"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"text":{"type":"string","maxLength":12000}}};

function validate381(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate381.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.id === undefined) && (missing0 = "id"))) || ((data.text === undefined) && (missing0 = "text"))){
validate381.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("guidance.update" !== data.type){
validate381.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "guidance.update"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate381.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.id !== undefined){
const _errs3 = errors;
if(!(validate296(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate296.errors : vErrors.concat(validate296.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.text !== undefined){
let data3 = data.text;
const _errs4 = errors;
if(errors === _errs4){
if(typeof data3 === "string"){
if(func1(data3) > 12000){
validate381.errors = [{instancePath:instancePath+"/text",schemaPath:"#/properties/text/maxLength",keyword:"maxLength",params:{limit: 12000},message:"must NOT have more than 12000 characters"}];
return false;
}
}
else {
validate381.errors = [{instancePath:instancePath+"/text",schemaPath:"#/properties/text/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
else {
validate381.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate381.errors = vErrors;
return errors === 0;
}
validate381.evaluated = {"props":{"type":true,"v":true,"id":true,"text":true},"dynamicProps":false,"dynamicItems":false};

const schema127 = {"type":"object","required":["type","v","id"],"properties":{"type":{"const":"guidance.reset"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"}}};

function validate383(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate383.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.id === undefined) && (missing0 = "id"))){
validate383.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("guidance.reset" !== data.type){
validate383.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "guidance.reset"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate383.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.id !== undefined){
const _errs3 = errors;
if(!(validate296(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate296.errors : vErrors.concat(validate296.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
else {
validate383.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate383.errors = vErrors;
return errors === 0;
}
validate383.evaluated = {"props":{"type":true,"v":true,"id":true},"dynamicProps":false,"dynamicItems":false};

const schema128 = {"type":"object","required":["type","v","id","about"],"properties":{"type":{"const":"context.update"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"about":{"type":"string","maxLength":4000}}};

function validate385(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate385.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.id === undefined) && (missing0 = "id"))) || ((data.about === undefined) && (missing0 = "about"))){
validate385.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("context.update" !== data.type){
validate385.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "context.update"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate385.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.id !== undefined){
const _errs3 = errors;
if(!(validate296(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate296.errors : vErrors.concat(validate296.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.about !== undefined){
let data3 = data.about;
const _errs4 = errors;
if(errors === _errs4){
if(typeof data3 === "string"){
if(func1(data3) > 4000){
validate385.errors = [{instancePath:instancePath+"/about",schemaPath:"#/properties/about/maxLength",keyword:"maxLength",params:{limit: 4000},message:"must NOT have more than 4000 characters"}];
return false;
}
}
else {
validate385.errors = [{instancePath:instancePath+"/about",schemaPath:"#/properties/about/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
else {
validate385.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate385.errors = vErrors;
return errors === 0;
}
validate385.evaluated = {"props":{"type":true,"v":true,"id":true,"about":true},"dynamicProps":false,"dynamicItems":false};

const schema129 = {"type":"object","required":["type","v","id","name","content_b64"],"properties":{"type":{"const":"context.upload"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"name":{"$ref":"#/$defs/FileName"},"content_b64":{"$ref":"#/$defs/Base64Data","type":"string","maxLength":6990508}}};

function validate387(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate387.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.id === undefined) && (missing0 = "id"))) || ((data.name === undefined) && (missing0 = "name"))) || ((data.content_b64 === undefined) && (missing0 = "content_b64"))){
validate387.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("context.upload" !== data.type){
validate387.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "context.upload"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate387.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.id !== undefined){
const _errs3 = errors;
if(!(validate296(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate296.errors : vErrors.concat(validate296.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.name !== undefined){
const _errs4 = errors;
if(!(validate369(data.name, {instancePath:instancePath+"/name",parentData:data,parentDataProperty:"name",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate369.errors : vErrors.concat(validate369.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.content_b64 !== undefined){
let data4 = data.content_b64;
const _errs5 = errors;
if(!(validate371(data4, {instancePath:instancePath+"/content_b64",parentData:data,parentDataProperty:"content_b64",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate371.errors : vErrors.concat(validate371.errors);
errors = vErrors.length;
}
if(errors === _errs5){
if(typeof data4 === "string"){
if(func1(data4) > 6990508){
validate387.errors = [{instancePath:instancePath+"/content_b64",schemaPath:"#/properties/content_b64/maxLength",keyword:"maxLength",params:{limit: 6990508},message:"must NOT have more than 6990508 characters"}];
return false;
}
}
else {
validate387.errors = [{instancePath:instancePath+"/content_b64",schemaPath:"#/properties/content_b64/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
else {
validate387.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate387.errors = vErrors;
return errors === 0;
}
validate387.evaluated = {"props":{"type":true,"v":true,"id":true,"name":true,"content_b64":true},"dynamicProps":false,"dynamicItems":false};

const schema130 = {"type":"object","required":["type","v","id","doc_id"],"properties":{"type":{"const":"context.remove"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"doc_id":{"$ref":"#/$defs/DocId"}}};

function validate391(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate391.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.id === undefined) && (missing0 = "id"))) || ((data.doc_id === undefined) && (missing0 = "doc_id"))){
validate391.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("context.remove" !== data.type){
validate391.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "context.remove"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate391.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.id !== undefined){
const _errs3 = errors;
if(!(validate296(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate296.errors : vErrors.concat(validate296.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.doc_id !== undefined){
const _errs4 = errors;
if(!(validate253(data.doc_id, {instancePath:instancePath+"/doc_id",parentData:data,parentDataProperty:"doc_id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate253.errors : vErrors.concat(validate253.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
else {
validate391.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate391.errors = vErrors;
return errors === 0;
}
validate391.evaluated = {"props":{"type":true,"v":true,"id":true,"doc_id":true},"dynamicProps":false,"dynamicItems":false};

const schema131 = {"type":"object","required":["type","v","id","decision_id"],"properties":{"type":{"const":"learning.forget"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"},"decision_id":{"$ref":"#/$defs/DecisionId"}}};

function validate394(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate394.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.id === undefined) && (missing0 = "id"))) || ((data.decision_id === undefined) && (missing0 = "decision_id"))){
validate394.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("learning.forget" !== data.type){
validate394.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "learning.forget"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate394.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.id !== undefined){
const _errs3 = errors;
if(!(validate296(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate296.errors : vErrors.concat(validate296.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.decision_id !== undefined){
const _errs4 = errors;
if(!(validate262(data.decision_id, {instancePath:instancePath+"/decision_id",parentData:data,parentDataProperty:"decision_id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate262.errors : vErrors.concat(validate262.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
else {
validate394.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate394.errors = vErrors;
return errors === 0;
}
validate394.evaluated = {"props":{"type":true,"v":true,"id":true,"decision_id":true},"dynamicProps":false,"dynamicItems":false};

const schema132 = {"type":"object","required":["type","v","id"],"properties":{"type":{"const":"learning.forget_all"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"}}};

function validate397(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate397.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.id === undefined) && (missing0 = "id"))){
validate397.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("learning.forget_all" !== data.type){
validate397.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "learning.forget_all"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate397.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.id !== undefined){
const _errs3 = errors;
if(!(validate296(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate296.errors : vErrors.concat(validate296.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
else {
validate397.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate397.errors = vErrors;
return errors === 0;
}
validate397.evaluated = {"props":{"type":true,"v":true,"id":true},"dynamicProps":false,"dynamicItems":false};

const schema133 = {"type":"object","required":["type","v","id"],"properties":{"type":{"const":"unpair"},"v":{"const":1},"id":{"$ref":"#/$defs/MessageId"}}};

function validate399(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate399.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.id === undefined) && (missing0 = "id"))){
validate399.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("unpair" !== data.type){
validate399.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "unpair"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate399.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.id !== undefined){
const _errs3 = errors;
if(!(validate296(data.id, {instancePath:instancePath+"/id",parentData:data,parentDataProperty:"id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate296.errors : vErrors.concat(validate296.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
else {
validate399.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate399.errors = vErrors;
return errors === 0;
}
validate399.evaluated = {"props":{"type":true,"v":true,"id":true},"dynamicProps":false,"dynamicItems":false};

const schema134 = {"type":"object","required":["type","v","chunk_id","index","count","data"],"properties":{"type":{"const":"chunk"},"v":{"const":1},"chunk_id":{"$ref":"#/$defs/MessageId"},"index":{"type":"integer","minimum":0,"maximum":255},"count":{"type":"integer","minimum":1,"maximum":256},"data":{"$ref":"#/$defs/Base64Data","type":"string","maxLength":43692}}};

function validate401(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate401.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((((data.type === undefined) && (missing0 = "type")) || ((data.v === undefined) && (missing0 = "v"))) || ((data.chunk_id === undefined) && (missing0 = "chunk_id"))) || ((data.index === undefined) && (missing0 = "index"))) || ((data.count === undefined) && (missing0 = "count"))) || ((data.data === undefined) && (missing0 = "data"))){
validate401.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.type !== undefined){
const _errs1 = errors;
if("chunk" !== data.type){
validate401.errors = [{instancePath:instancePath+"/type",schemaPath:"#/properties/type/const",keyword:"const",params:{allowedValue: "chunk"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.v !== undefined){
const _errs2 = errors;
if(1 !== data.v){
validate401.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.chunk_id !== undefined){
const _errs3 = errors;
if(!(validate296(data.chunk_id, {instancePath:instancePath+"/chunk_id",parentData:data,parentDataProperty:"chunk_id",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate296.errors : vErrors.concat(validate296.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.index !== undefined){
let data3 = data.index;
const _errs4 = errors;
if(!(((typeof data3 == "number") && (!(data3 % 1) && !isNaN(data3))) && (isFinite(data3)))){
validate401.errors = [{instancePath:instancePath+"/index",schemaPath:"#/properties/index/type",keyword:"type",params:{type: "integer"},message:"must be integer"}];
return false;
}
if(errors === _errs4){
if((typeof data3 == "number") && (isFinite(data3))){
if(data3 > 255 || isNaN(data3)){
validate401.errors = [{instancePath:instancePath+"/index",schemaPath:"#/properties/index/maximum",keyword:"maximum",params:{comparison: "<=", limit: 255},message:"must be <= 255"}];
return false;
}
else {
if(data3 < 0 || isNaN(data3)){
validate401.errors = [{instancePath:instancePath+"/index",schemaPath:"#/properties/index/minimum",keyword:"minimum",params:{comparison: ">=", limit: 0},message:"must be >= 0"}];
return false;
}
}
}
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.count !== undefined){
let data4 = data.count;
const _errs6 = errors;
if(!(((typeof data4 == "number") && (!(data4 % 1) && !isNaN(data4))) && (isFinite(data4)))){
validate401.errors = [{instancePath:instancePath+"/count",schemaPath:"#/properties/count/type",keyword:"type",params:{type: "integer"},message:"must be integer"}];
return false;
}
if(errors === _errs6){
if((typeof data4 == "number") && (isFinite(data4))){
if(data4 > 256 || isNaN(data4)){
validate401.errors = [{instancePath:instancePath+"/count",schemaPath:"#/properties/count/maximum",keyword:"maximum",params:{comparison: "<=", limit: 256},message:"must be <= 256"}];
return false;
}
else {
if(data4 < 1 || isNaN(data4)){
validate401.errors = [{instancePath:instancePath+"/count",schemaPath:"#/properties/count/minimum",keyword:"minimum",params:{comparison: ">=", limit: 1},message:"must be >= 1"}];
return false;
}
}
}
}
var valid0 = _errs6 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.data !== undefined){
let data5 = data.data;
const _errs8 = errors;
if(!(validate371(data5, {instancePath:instancePath+"/data",parentData:data,parentDataProperty:"data",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate371.errors : vErrors.concat(validate371.errors);
errors = vErrors.length;
}
if(errors === _errs8){
if(typeof data5 === "string"){
if(func1(data5) > 43692){
validate401.errors = [{instancePath:instancePath+"/data",schemaPath:"#/properties/data/maxLength",keyword:"maxLength",params:{limit: 43692},message:"must NOT have more than 43692 characters"}];
return false;
}
}
else {
validate401.errors = [{instancePath:instancePath+"/data",schemaPath:"#/properties/data/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs8 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
else {
validate401.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate401.errors = vErrors;
return errors === 0;
}
validate401.evaluated = {"props":{"type":true,"v":true,"chunk_id":true,"index":true,"count":true,"data":true},"dynamicProps":false,"dynamicItems":false};


function validate52(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate52.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
const tag0 = data.type;
if(typeof tag0 == "string"){
if(tag0 === "status"){
if(!(validate53(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate53.errors : vErrors.concat(validate53.errors);
errors = vErrors.length;
}
var props0 = {};
props0.type = true;
props0.v = true;
props0.cli_version = true;
props0.contract_version = true;
props0.machine = true;
props0.paired_at = true;
props0.last_run = true;
props0.next_run_at = true;
props0.running = true;
props0.budget_paused = true;
}
else if(tag0 === "ads.snapshot"){
if(!(validate74(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate74.errors : vErrors.concat(validate74.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.generated_at = true;
props0.as_of = true;
props0.active = true;
props0.active_total = true;
props0.hidden = true;
props0.hidden_total = true;
props0.hidden_counts = true;
props0.closed = true;
props0.closed_total = true;
}
}
else if(tag0 === "applications.snapshot"){
if(!(validate142(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate142.errors : vErrors.concat(validate142.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.generated_at = true;
props0.applications = true;
props0.applications_total = true;
}
}
else if(tag0 === "config.snapshot"){
if(!(validate166(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate166.errors : vErrors.concat(validate166.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.roles = true;
props0.targets = true;
props0.sources = true;
props0.schedule = true;
props0.notify = true;
props0.model = true;
props0.language = true;
}
}
else if(tag0 === "profile.snapshot"){
if(!(validate212(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate212.errors : vErrors.concat(validate212.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.cv_name = true;
props0.cv_read_at = true;
props0.summary = true;
props0.education = true;
props0.languages = true;
props0.stack = true;
props0.projects = true;
}
}
else if(tag0 === "usage.snapshot"){
if(!(validate221(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate221.errors : vErrors.concat(validate221.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.month = true;
props0.month_tokens = true;
props0.today_tokens = true;
props0.month_usd = true;
props0.today_usd = true;
props0.resets_on = true;
props0.days = true;
}
}
else if(tag0 === "models.snapshot"){
if(!(validate236(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate236.errors : vErrors.concat(validate236.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.providers = true;
}
}
else if(tag0 === "guidance.snapshot"){
if(!(validate247(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate247.errors : vErrors.concat(validate247.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.text = true;
props0.is_default = true;
props0.limit = true;
}
}
else if(tag0 === "context.snapshot"){
if(!(validate250(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate250.errors : vErrors.concat(validate250.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.about = true;
props0.about_limit = true;
props0.docs = true;
props0.docs_limit = true;
}
}
else if(tag0 === "learning.snapshot"){
if(!(validate259(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate259.errors : vErrors.concat(validate259.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.state = true;
props0.unavailable_reason = true;
props0.decision_count = true;
props0.cold_start_threshold = true;
props0.examples = true;
props0.examples_total = true;
}
}
else if(tag0 === "run.progress"){
if(!(validate270(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate270.errors : vErrors.concat(validate270.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.run_id = true;
props0.mode = true;
props0.started_at = true;
props0.percent = true;
props0.sources = true;
props0.model = true;
}
}
else if(tag0 === "run.finished"){
if(!(validate283(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate283.errors : vErrors.concat(validate283.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.run_id = true;
props0.finished_at = true;
props0.ads_read = true;
props0.new_ads = true;
props0.matches = true;
props0.hidden = true;
props0.cost_usd = true;
props0.failed_sources = true;
props0.budget_paused = true;
}
}
else if(tag0 === "run.cancelled"){
if(!(validate292(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate292.errors : vErrors.concat(validate292.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.run_id = true;
props0.at = true;
}
}
else if(tag0 === "saved"){
if(!(validate295(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate295.errors : vErrors.concat(validate295.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.ref = true;
props0.applies_from = true;
props0.recheck = true;
}
}
else if(tag0 === "error"){
if(!(validate302(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate302.errors : vErrors.concat(validate302.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.ref = true;
props0.code = true;
props0.message = true;
props0.field = true;
}
}
else if(tag0 === "unpaired"){
if(!(validate304(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate304.errors : vErrors.concat(validate304.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.reason = true;
}
}
else if(tag0 === "run.start"){
if(!(validate305(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate305.errors : vErrors.concat(validate305.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.id = true;
}
}
else if(tag0 === "run.cancel"){
if(!(validate307(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate307.errors : vErrors.concat(validate307.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.id = true;
props0.run_id = true;
}
}
else if(tag0 === "ad.mark"){
if(!(validate310(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate310.errors : vErrors.concat(validate310.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.id = true;
props0.ad_id = true;
props0.mark = true;
}
}
else if(tag0 === "ad.unmark"){
if(!(validate314(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate314.errors : vErrors.concat(validate314.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.id = true;
props0.ad_id = true;
}
}
else if(tag0 === "ad.show_anyway"){
if(!(validate317(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate317.errors : vErrors.concat(validate317.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.id = true;
props0.ad_id = true;
props0.on = true;
}
}
else if(tag0 === "application.create"){
if(!(validate320(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate320.errors : vErrors.concat(validate320.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.id = true;
props0.company = true;
props0.title = true;
props0.note = true;
}
}
else if(tag0 === "application.update"){
if(!(validate324(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate324.errors : vErrors.concat(validate324.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.id = true;
props0.application_id = true;
props0.status = true;
props0.note = true;
}
}
else if(tag0 === "application.remove"){
if(!(validate329(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate329.errors : vErrors.concat(validate329.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.id = true;
props0.application_id = true;
props0.removed = true;
}
}
else if(tag0 === "config.update"){
if(!(validate332(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate332.errors : vErrors.concat(validate332.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.id = true;
props0.patch = true;
}
}
else if(tag0 === "model.select"){
if(!(validate360(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate360.errors : vErrors.concat(validate360.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.id = true;
props0.provider = true;
props0.model = true;
}
}
else if(tag0 === "key.replace"){
if(!(validate364(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate364.errors : vErrors.concat(validate364.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.id = true;
props0.provider = true;
props0.key = true;
}
}
else if(tag0 === "cv.upload"){
if(!(validate367(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate367.errors : vErrors.concat(validate367.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.id = true;
props0.name = true;
props0.size_bytes = true;
props0.content_b64 = true;
}
}
else if(tag0 === "profile.update"){
if(!(validate373(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate373.errors : vErrors.concat(validate373.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.id = true;
props0.summary = true;
props0.education = true;
props0.languages = true;
props0.stack = true;
props0.projects = true;
}
}
else if(tag0 === "guidance.update"){
if(!(validate381(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate381.errors : vErrors.concat(validate381.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.id = true;
props0.text = true;
}
}
else if(tag0 === "guidance.reset"){
if(!(validate383(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate383.errors : vErrors.concat(validate383.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.id = true;
}
}
else if(tag0 === "context.update"){
if(!(validate385(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate385.errors : vErrors.concat(validate385.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.id = true;
props0.about = true;
}
}
else if(tag0 === "context.upload"){
if(!(validate387(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate387.errors : vErrors.concat(validate387.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.id = true;
props0.name = true;
props0.content_b64 = true;
}
}
else if(tag0 === "context.remove"){
if(!(validate391(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate391.errors : vErrors.concat(validate391.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.id = true;
props0.doc_id = true;
}
}
else if(tag0 === "learning.forget"){
if(!(validate394(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate394.errors : vErrors.concat(validate394.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.id = true;
props0.decision_id = true;
}
}
else if(tag0 === "learning.forget_all"){
if(!(validate397(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate397.errors : vErrors.concat(validate397.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.id = true;
}
}
else if(tag0 === "unpair"){
if(!(validate399(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate399.errors : vErrors.concat(validate399.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.id = true;
}
}
else if(tag0 === "chunk"){
if(!(validate401(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate401.errors : vErrors.concat(validate401.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.type = true;
props0.v = true;
props0.chunk_id = true;
props0.index = true;
props0.count = true;
props0.data = true;
}
}
else {
validate52.errors = [{instancePath,schemaPath:"#/discriminator",keyword:"discriminator",params:{error: "mapping", tag: "type", tagValue: tag0},message:"value of tag \"type\" must be in oneOf"}];
return false;
}
}
else {
validate52.errors = [{instancePath,schemaPath:"#/discriminator",keyword:"discriminator",params:{error: "tag", tag: "type", tagValue: tag0},message:"tag \"type\" must be string"}];
return false;
}
}
else {
validate52.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate52.errors = vErrors;
evaluated0.props = props0;
return errors === 0;
}
validate52.evaluated = {"dynamicProps":true,"dynamicItems":false};

export const validateEnvelope = validate442;
const schema135 = {"$schema":"https://json-schema.org/draft/2020-12/schema","title":"Envelope","type":"object","oneOf":[{"$ref":"#/$defs/EnvelopeHello"},{"$ref":"#/$defs/EnvelopeData"}],"discriminator":{"propertyName":"kind"},"$defs":{"Channel":{"type":"string","pattern":"^[A-Za-z0-9_-]{42}[AEIMQUYcgkosw048]$","maxLength":43},"Sid":{"type":"string","pattern":"^[A-Za-z0-9_-]{21}[AQgw]$","maxLength":22},"Seq":{"type":"integer","minimum":1,"maximum":4294967295},"Base64Url":{"type":"string","pattern":"^(?:[A-Za-z0-9_-]{4})*(?:[A-Za-z0-9_-][AQgw]|[A-Za-z0-9_-]{2}[AEIMQUYcgkosw048])?$","not":{"pattern":"[^A-Za-z0-9_-]"}},"EnvelopeHello":{"type":"object","required":["v","channel","sid","seq","kind","ciphertext"],"properties":{"v":{"const":1},"channel":{"$ref":"#/$defs/Channel"},"sid":{"$ref":"#/$defs/Sid"},"seq":{"const":0},"kind":{"const":"hello"},"ciphertext":{"$ref":"#/$defs/Base64Url","type":"string","minLength":4,"maxLength":5462}}},"EnvelopeData":{"type":"object","required":["v","channel","sid","seq","kind","ciphertext"],"properties":{"v":{"const":1},"channel":{"$ref":"#/$defs/Channel"},"sid":{"$ref":"#/$defs/Sid"},"seq":{"$ref":"#/$defs/Seq"},"kind":{"const":"data"},"ciphertext":{"$ref":"#/$defs/Base64Url","type":"string","minLength":22,"maxLength":61440}}}}};
const schema136 = {"type":"object","required":["v","channel","sid","seq","kind","ciphertext"],"properties":{"v":{"const":1},"channel":{"$ref":"#/$defs/Channel"},"sid":{"$ref":"#/$defs/Sid"},"seq":{"const":0},"kind":{"const":"hello"},"ciphertext":{"$ref":"#/$defs/Base64Url","type":"string","minLength":4,"maxLength":5462}}};
const schema137 = {"type":"string","pattern":"^[A-Za-z0-9_-]{42}[AEIMQUYcgkosw048]$","maxLength":43};
const pattern24 = new RegExp("^[A-Za-z0-9_-]{42}[AEIMQUYcgkosw048]$", "u");

function validate444(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate444.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(typeof data === "string"){
if(func1(data) > 43){
validate444.errors = [{instancePath,schemaPath:"#/maxLength",keyword:"maxLength",params:{limit: 43},message:"must NOT have more than 43 characters"}];
return false;
}
else {
if(!pattern24.test(data)){
validate444.errors = [{instancePath,schemaPath:"#/pattern",keyword:"pattern",params:{pattern: "^[A-Za-z0-9_-]{42}[AEIMQUYcgkosw048]$"},message:"must match pattern \""+"^[A-Za-z0-9_-]{42}[AEIMQUYcgkosw048]$"+"\""}];
return false;
}
}
}
else {
validate444.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
validate444.errors = vErrors;
return errors === 0;
}
validate444.evaluated = {"dynamicProps":false,"dynamicItems":false};

const schema138 = {"type":"string","pattern":"^[A-Za-z0-9_-]{21}[AQgw]$","maxLength":22};
const pattern25 = new RegExp("^[A-Za-z0-9_-]{21}[AQgw]$", "u");

function validate446(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate446.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(typeof data === "string"){
if(func1(data) > 22){
validate446.errors = [{instancePath,schemaPath:"#/maxLength",keyword:"maxLength",params:{limit: 22},message:"must NOT have more than 22 characters"}];
return false;
}
else {
if(!pattern25.test(data)){
validate446.errors = [{instancePath,schemaPath:"#/pattern",keyword:"pattern",params:{pattern: "^[A-Za-z0-9_-]{21}[AQgw]$"},message:"must match pattern \""+"^[A-Za-z0-9_-]{21}[AQgw]$"+"\""}];
return false;
}
}
}
else {
validate446.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
validate446.errors = vErrors;
return errors === 0;
}
validate446.evaluated = {"dynamicProps":false,"dynamicItems":false};

const schema139 = {"type":"string","pattern":"^(?:[A-Za-z0-9_-]{4})*(?:[A-Za-z0-9_-][AQgw]|[A-Za-z0-9_-]{2}[AEIMQUYcgkosw048])?$","not":{"pattern":"[^A-Za-z0-9_-]"}};
const pattern26 = new RegExp("[^A-Za-z0-9_-]", "u");
const pattern27 = new RegExp("^(?:[A-Za-z0-9_-]{4})*(?:[A-Za-z0-9_-][AQgw]|[A-Za-z0-9_-]{2}[AEIMQUYcgkosw048])?$", "u");

function validate448(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate448.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
const _errs1 = errors;
const _errs2 = errors;
if(typeof data === "string"){
if(!pattern26.test(data)){
const err0 = {};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
}
var valid0 = _errs2 === errors;
if(valid0){
validate448.errors = [{instancePath,schemaPath:"#/not",keyword:"not",params:{},message:"must NOT be valid"}];
return false;
}
else {
errors = _errs1;
if(vErrors !== null){
if(_errs1){
vErrors.length = _errs1;
}
else {
vErrors = null;
}
}
}
if(errors === 0){
if(typeof data === "string"){
if(!pattern27.test(data)){
validate448.errors = [{instancePath,schemaPath:"#/pattern",keyword:"pattern",params:{pattern: "^(?:[A-Za-z0-9_-]{4})*(?:[A-Za-z0-9_-][AQgw]|[A-Za-z0-9_-]{2}[AEIMQUYcgkosw048])?$"},message:"must match pattern \""+"^(?:[A-Za-z0-9_-]{4})*(?:[A-Za-z0-9_-][AQgw]|[A-Za-z0-9_-]{2}[AEIMQUYcgkosw048])?$"+"\""}];
return false;
}
}
else {
validate448.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
validate448.errors = vErrors;
return errors === 0;
}
validate448.evaluated = {"dynamicProps":false,"dynamicItems":false};


function validate443(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate443.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((((data.v === undefined) && (missing0 = "v")) || ((data.channel === undefined) && (missing0 = "channel"))) || ((data.sid === undefined) && (missing0 = "sid"))) || ((data.seq === undefined) && (missing0 = "seq"))) || ((data.kind === undefined) && (missing0 = "kind"))) || ((data.ciphertext === undefined) && (missing0 = "ciphertext"))){
validate443.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.v !== undefined){
const _errs1 = errors;
if(1 !== data.v){
validate443.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.channel !== undefined){
const _errs2 = errors;
if(!(validate444(data.channel, {instancePath:instancePath+"/channel",parentData:data,parentDataProperty:"channel",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate444.errors : vErrors.concat(validate444.errors);
errors = vErrors.length;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.sid !== undefined){
const _errs3 = errors;
if(!(validate446(data.sid, {instancePath:instancePath+"/sid",parentData:data,parentDataProperty:"sid",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate446.errors : vErrors.concat(validate446.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.seq !== undefined){
const _errs4 = errors;
if(0 !== data.seq){
validate443.errors = [{instancePath:instancePath+"/seq",schemaPath:"#/properties/seq/const",keyword:"const",params:{allowedValue: 0},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.kind !== undefined){
const _errs5 = errors;
if("hello" !== data.kind){
validate443.errors = [{instancePath:instancePath+"/kind",schemaPath:"#/properties/kind/const",keyword:"const",params:{allowedValue: "hello"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.ciphertext !== undefined){
let data5 = data.ciphertext;
const _errs6 = errors;
if(!(validate448(data5, {instancePath:instancePath+"/ciphertext",parentData:data,parentDataProperty:"ciphertext",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate448.errors : vErrors.concat(validate448.errors);
errors = vErrors.length;
}
if(errors === _errs6){
if(typeof data5 === "string"){
if(func1(data5) > 5462){
validate443.errors = [{instancePath:instancePath+"/ciphertext",schemaPath:"#/properties/ciphertext/maxLength",keyword:"maxLength",params:{limit: 5462},message:"must NOT have more than 5462 characters"}];
return false;
}
else {
if(func1(data5) < 4){
validate443.errors = [{instancePath:instancePath+"/ciphertext",schemaPath:"#/properties/ciphertext/minLength",keyword:"minLength",params:{limit: 4},message:"must NOT have fewer than 4 characters"}];
return false;
}
}
}
else {
validate443.errors = [{instancePath:instancePath+"/ciphertext",schemaPath:"#/properties/ciphertext/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs6 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
else {
validate443.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate443.errors = vErrors;
return errors === 0;
}
validate443.evaluated = {"props":{"v":true,"channel":true,"sid":true,"seq":true,"kind":true,"ciphertext":true},"dynamicProps":false,"dynamicItems":false};

const schema140 = {"type":"object","required":["v","channel","sid","seq","kind","ciphertext"],"properties":{"v":{"const":1},"channel":{"$ref":"#/$defs/Channel"},"sid":{"$ref":"#/$defs/Sid"},"seq":{"$ref":"#/$defs/Seq"},"kind":{"const":"data"},"ciphertext":{"$ref":"#/$defs/Base64Url","type":"string","minLength":22,"maxLength":61440}}};
const schema141 = {"type":"integer","minimum":1,"maximum":4294967295};

function validate453(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate453.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(!(((typeof data == "number") && (!(data % 1) && !isNaN(data))) && (isFinite(data)))){
validate453.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "integer"},message:"must be integer"}];
return false;
}
if(errors === 0){
if((typeof data == "number") && (isFinite(data))){
if(data > 4294967295 || isNaN(data)){
validate453.errors = [{instancePath,schemaPath:"#/maximum",keyword:"maximum",params:{comparison: "<=", limit: 4294967295},message:"must be <= 4294967295"}];
return false;
}
else {
if(data < 1 || isNaN(data)){
validate453.errors = [{instancePath,schemaPath:"#/minimum",keyword:"minimum",params:{comparison: ">=", limit: 1},message:"must be >= 1"}];
return false;
}
}
}
}
validate453.errors = vErrors;
return errors === 0;
}
validate453.evaluated = {"dynamicProps":false,"dynamicItems":false};


function validate450(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate450.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if(((((((data.v === undefined) && (missing0 = "v")) || ((data.channel === undefined) && (missing0 = "channel"))) || ((data.sid === undefined) && (missing0 = "sid"))) || ((data.seq === undefined) && (missing0 = "seq"))) || ((data.kind === undefined) && (missing0 = "kind"))) || ((data.ciphertext === undefined) && (missing0 = "ciphertext"))){
validate450.errors = [{instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: missing0},message:"must have required property '"+missing0+"'"}];
return false;
}
else {
if(data.v !== undefined){
const _errs1 = errors;
if(1 !== data.v){
validate450.errors = [{instancePath:instancePath+"/v",schemaPath:"#/properties/v/const",keyword:"const",params:{allowedValue: 1},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs1 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.channel !== undefined){
const _errs2 = errors;
if(!(validate444(data.channel, {instancePath:instancePath+"/channel",parentData:data,parentDataProperty:"channel",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate444.errors : vErrors.concat(validate444.errors);
errors = vErrors.length;
}
var valid0 = _errs2 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.sid !== undefined){
const _errs3 = errors;
if(!(validate446(data.sid, {instancePath:instancePath+"/sid",parentData:data,parentDataProperty:"sid",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate446.errors : vErrors.concat(validate446.errors);
errors = vErrors.length;
}
var valid0 = _errs3 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.seq !== undefined){
const _errs4 = errors;
if(!(validate453(data.seq, {instancePath:instancePath+"/seq",parentData:data,parentDataProperty:"seq",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate453.errors : vErrors.concat(validate453.errors);
errors = vErrors.length;
}
var valid0 = _errs4 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.kind !== undefined){
const _errs5 = errors;
if("data" !== data.kind){
validate450.errors = [{instancePath:instancePath+"/kind",schemaPath:"#/properties/kind/const",keyword:"const",params:{allowedValue: "data"},message:"must be equal to constant"}];
return false;
}
var valid0 = _errs5 === errors;
}
else {
var valid0 = true;
}
if(valid0){
if(data.ciphertext !== undefined){
let data5 = data.ciphertext;
const _errs6 = errors;
if(!(validate448(data5, {instancePath:instancePath+"/ciphertext",parentData:data,parentDataProperty:"ciphertext",rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate448.errors : vErrors.concat(validate448.errors);
errors = vErrors.length;
}
if(errors === _errs6){
if(typeof data5 === "string"){
if(func1(data5) > 61440){
validate450.errors = [{instancePath:instancePath+"/ciphertext",schemaPath:"#/properties/ciphertext/maxLength",keyword:"maxLength",params:{limit: 61440},message:"must NOT have more than 61440 characters"}];
return false;
}
else {
if(func1(data5) < 22){
validate450.errors = [{instancePath:instancePath+"/ciphertext",schemaPath:"#/properties/ciphertext/minLength",keyword:"minLength",params:{limit: 22},message:"must NOT have fewer than 22 characters"}];
return false;
}
}
}
else {
validate450.errors = [{instancePath:instancePath+"/ciphertext",schemaPath:"#/properties/ciphertext/type",keyword:"type",params:{type: "string"},message:"must be string"}];
return false;
}
}
var valid0 = _errs6 === errors;
}
else {
var valid0 = true;
}
}
}
}
}
}
}
}
else {
validate450.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate450.errors = vErrors;
return errors === 0;
}
validate450.evaluated = {"props":{"v":true,"channel":true,"sid":true,"seq":true,"kind":true,"ciphertext":true},"dynamicProps":false,"dynamicItems":false};


function validate442(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate442.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(errors === 0){
if(data && typeof data == "object" && !Array.isArray(data)){
const tag0 = data.kind;
if(typeof tag0 == "string"){
if(tag0 === "hello"){
if(!(validate443(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate443.errors : vErrors.concat(validate443.errors);
errors = vErrors.length;
}
var props0 = {};
props0.v = true;
props0.channel = true;
props0.sid = true;
props0.seq = true;
props0.kind = true;
props0.ciphertext = true;
}
else if(tag0 === "data"){
if(!(validate450(data, {instancePath,parentData,parentDataProperty,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate450.errors : vErrors.concat(validate450.errors);
errors = vErrors.length;
}
if(props0 !== true){
props0 = props0 || {};
props0.v = true;
props0.channel = true;
props0.sid = true;
props0.seq = true;
props0.kind = true;
props0.ciphertext = true;
}
}
else {
validate442.errors = [{instancePath,schemaPath:"#/discriminator",keyword:"discriminator",params:{error: "mapping", tag: "kind", tagValue: tag0},message:"value of tag \"kind\" must be in oneOf"}];
return false;
}
}
else {
validate442.errors = [{instancePath,schemaPath:"#/discriminator",keyword:"discriminator",params:{error: "tag", tag: "kind", tagValue: tag0},message:"tag \"kind\" must be string"}];
return false;
}
}
else {
validate442.errors = [{instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"}];
return false;
}
}
validate442.errors = vErrors;
evaluated0.props = props0;
return errors === 0;
}
validate442.evaluated = {"dynamicProps":true,"dynamicItems":false};

