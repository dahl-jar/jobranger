export type Payload =
  | Status
  | AdsSnapshot
  | ApplicationsSnapshot
  | ConfigSnapshot
  | ProfileSnapshot
  | UsageSnapshot
  | ModelsSnapshot
  | GuidanceSnapshot
  | ContextSnapshot
  | LearningSnapshot
  | RunProgress
  | RunFinished
  | RunCancelled
  | Saved
  | ErrorMessage
  | Unpaired
  | RunStart
  | RunCancel
  | AdMark
  | AdUnmark
  | AdShowAnyway
  | ApplicationCreate
  | ApplicationUpdate
  | ApplicationRemove
  | ConfigUpdate
  | ModelSelect
  | KeyReplace
  | CvUpload
  | ProfileUpdate
  | GuidanceUpdate
  | GuidanceReset
  | ContextUpdate
  | ContextUpload
  | ContextRemove
  | LearningForget
  | LearningForgetAll
  | Unpair
  | Chunk;
export type Timestamp = string;
export type RunId = number;
export type Count = number;
export type Money = number;
export type RunMode = "scheduled" | "manual" | "dry_run";
export type Day = string;
export type AdId = string;
export type Company = string;
export type AdTitle = string;
export type RoleType = RoleId | "general_software";
export type RoleId =
  | "backend"
  | "frontend"
  | "fullstack"
  | "mobile"
  | "data_engineering"
  | "devops_platform"
  | "ml_ai"
  | "embedded"
  | "game";
export type SourceId = "nav" | "finn" | "linkedin" | "teamtailor" | "ashby";
export type Url = string;
export type Fit = "strong" | "partial" | "on_request";
export type ShortText = string;
export type CheckState = "met" | "partly" | "missing";
export type Mark = "applied" | "skipped" | "not_for_me";
export type HiddenKind = "senior" | "years" | "masters" | "role" | "place" | "other";
export type ClosedReason = "deadline" | "removed" | "filled";
export type ClosedMark = "applied" | "skipped" | "not_for_me" | "none";
export type ApplicationId = number;
export type ApplicationStatus = "waiting" | "interview" | "offer" | "accepted" | "rejected";
export type Note = string;
export type RoleIds = RoleId[];
export type Level = "junior" | "mid" | "senior" | "lead" | "none";
export type Levels = Level[];
export type YearBand = "0-1" | "1-3" | "3-6" | "6+";
export type YearBands = YearBand[];
export type Places = string[];
export type ClockTime = string;
export type ClockTimes = ClockTime[];
export type Email = string;
export type ProviderId = "anthropic" | "openai" | "openrouter" | "ollama";
export type ModelId = string;
export type Terms = string[];
export type Limit = number;
export type DocId = string;
export type DecisionId = number;
export type MessageId = string;
export type FileName = string;

export interface Status {
  type: "status";
  v: 1;
  cli_version: string;
  contract_version: number;
  machine: string;
  paired_at: Timestamp;
  last_run: LastRun | null;
  next_run_at: Timestamp | null;
  running: ActiveRun | null;
  budget_paused: boolean;
}
export interface LastRun {
  run_id: RunId;
  finished_at: Timestamp;
  ads_read: Count;
  matches: Count;
  cost_usd: Money;
}
export interface ActiveRun {
  run_id: RunId;
  mode: RunMode;
  started_at: Timestamp;
  results_by: Timestamp | null;
}
export interface AdsSnapshot {
  type: "ads.snapshot";
  v: 1;
  generated_at: Timestamp;
  as_of: Day;
  active: ActiveAd[];
  active_total: Count;
  hidden: HiddenAd[];
  hidden_total: Count;
  hidden_counts: HiddenCounts;
  closed: ClosedAd[];
  closed_total: Count;
}
export interface ActiveAd {
  id: AdId;
  company: Company;
  title: AdTitle;
  place: string;
  role_type: RoleType;
  source: SourceId;
  published_at: Timestamp | null;
  found_at: Timestamp;
  url: Url;
  deadline: Day | null;
  fit: Fit;
  is_new: boolean;
  today: boolean;
  summary: string;
  reason: ShortText;
  gaps: ShortText[];
  checks: Check[];
  mark: Mark | null;
  marked_at: Timestamp | null;
  shown_anyway: boolean;
}
export interface Check {
  text: string;
  state: CheckState;
}
export interface HiddenAd {
  id: AdId;
  company: Company;
  title: AdTitle;
  place: string;
  role_type: RoleType | null;
  source: SourceId;
  url: Url;
  found_at: Timestamp;
  today: boolean;
  kind: HiddenKind;
  reason: string;
}
export interface HiddenCounts {
  senior: Count;
  years: Count;
  masters: Count;
  role: Count;
  place: Count;
  other: Count;
}
export interface ClosedAd {
  id: AdId;
  closed_on: Day;
  company: Company;
  title: AdTitle;
  source: SourceId;
  reason: ClosedReason;
  mark: ClosedMark;
}
export interface ApplicationsSnapshot {
  type: "applications.snapshot";
  v: 1;
  generated_at: Timestamp;
  applications: Application[];
  applications_total: Count;
}
export interface Application {
  id: ApplicationId;
  ad_id: AdId | null;
  company: Company;
  title: string;
  place: string;
  role_type: RoleType | null;
  source: SourceId | null;
  url: Url | null;
  deadline: Day | null;
  fit: Fit | null;
  status: ApplicationStatus;
  note: Note;
  applied_at: Timestamp;
  found_at: Timestamp | null;
  cv_name: string | null;
  checks: Check[];
  history: HistoryEntry[];
  removed: boolean;
}
export interface HistoryEntry {
  at: Timestamp;
  text: string;
}
export interface ConfigSnapshot {
  type: "config.snapshot";
  v: 1;
  roles: RoleOption[];
  targets: Targets;
  sources: Sources;
  schedule: Schedule;
  notify: Notify;
  model: ModelConfig;
}
export interface RoleOption {
  id: RoleId;
  label: string;
}
export interface Targets {
  subfields: RoleIds;
  levels: Levels;
  years: YearBands;
  hide_masters: boolean;
  places: Places;
  relocate: boolean;
}
export interface Sources {
  nav: boolean;
  employers: boolean;
  finn: boolean;
  linkedin: boolean;
}
export interface Schedule {
  times: ClockTimes;
  min_interval_hours: 2;
}
export interface Notify {
  website: boolean;
  email: EmailNotify;
}
export interface EmailNotify {
  on: boolean;
  address: Email | null;
}
export interface ModelConfig {
  provider: ProviderId | null;
  model: ModelId | null;
  key_hint: string | null;
  price: Price;
  monthly_limit_usd: Money | null;
}
export interface Price {
  input_per_mtok: Money;
  output_per_mtok: Money;
}
export interface ProfileSnapshot {
  type: "profile.snapshot";
  v: 1;
  cv_name: string | null;
  cv_read_at: Timestamp | null;
  summary: ShortText;
  education: ShortText;
  languages: Terms;
  stack: Terms;
  projects: ProjectRef[];
}
export interface ProjectRef {
  id: string;
  name: string;
}
export interface UsageSnapshot {
  type: "usage.snapshot";
  v: 1;
  month: string;
  month_tokens: Count;
  today_tokens: Count;
  month_usd: Money;
  today_usd: Money;
  resets_on: Day;
  days: UsageDay[];
}
export interface UsageDay {
  day: Day;
  calls: Count;
  input_tokens: Count;
  output_tokens: Count;
  cost_usd: Money;
  ads_read: Count;
  ads_checked: Count;
}
export interface ModelsSnapshot {
  type: "models.snapshot";
  v: 1;
  providers: ProviderModels[];
}
export interface ProviderModels {
  provider: ProviderId;
  error: ShortText | null;
  models: ModelInfo[];
}
export interface ModelInfo {
  id: ModelId;
  name: string;
  input_per_mtok: Money | null;
  output_per_mtok: Money | null;
  usd_per_day: Money | null;
}
export interface GuidanceSnapshot {
  type: "guidance.snapshot";
  v: 1;
  text: string;
  is_default: boolean;
  limit: Limit;
}
export interface ContextSnapshot {
  type: "context.snapshot";
  v: 1;
  about: string;
  about_limit: Limit;
  docs: ContextDoc[];
  docs_limit: Limit;
}
export interface ContextDoc {
  id: DocId;
  name: string;
  chars: Count;
  added_at: Day;
}
export interface LearningSnapshot {
  type: "learning.snapshot";
  v: 1;
  state: "unavailable" | "collecting" | "preparing" | "on";
  unavailable_reason: string | null;
  decision_count: Count;
  cold_start_threshold: 20;
  examples: LearningExample[];
  examples_total: Count;
}
export interface LearningExample {
  decision_id: DecisionId;
  ad_id: AdId;
  company: Company;
  title: AdTitle;
  decision: "applied" | "not_for_me" | "show_anyway";
  at: Timestamp;
}
export interface RunProgress {
  type: "run.progress";
  v: 1;
  run_id: RunId;
  mode: RunMode;
  started_at: Timestamp;
  percent: number;
  sources: SourceStep[];
  model: ModelStep;
}
export interface SourceStep {
  source: SourceId;
  state: "pending" | "active" | "done" | "failed";
  found_new: Count | null;
}
export interface ModelStep {
  state: "pending" | "active" | "done" | "skipped";
  checked: Count;
  total: Count;
  results_by: Timestamp | null;
}
export interface RunFinished {
  type: "run.finished";
  v: 1;
  run_id: RunId;
  finished_at: Timestamp;
  ads_read: Count;
  new_ads: Count;
  matches: Count;
  hidden: Count;
  cost_usd: Money;
  failed_sources: SourceId[];
  budget_paused: boolean;
}
export interface RunCancelled {
  type: "run.cancelled";
  v: 1;
  run_id: RunId;
  at: Timestamp;
}
export interface Saved {
  type: "saved";
  v: 1;
  ref: MessageId;
  applies_from: "now" | "next_run";
  recheck?: Recheck;
}
export interface Recheck {
  ads: Count;
  cost_usd: Money | null;
}
export interface ErrorMessage {
  type: "error";
  v: 1;
  ref: MessageId | null;
  code:
    | "invalid_payload"
    | "unknown_type"
    | "unsupported_version"
    | "not_found"
    | "run_in_progress"
    | "no_run"
    | "run_failed"
    | "key_rejected"
    | "cv_type"
    | "cv_unreadable"
    | "cv_no_text"
    | "cv_too_large"
    | "cv_parse_failed"
    | "chunk_incomplete"
    | "internal"
    | "guidance_empty"
    | "context_full"
    | "context_unreadable";
  message: string;
  field: string | null;
}
export interface Unpaired {
  type: "unpaired";
  v: 1;
  reason: "refused" | "web" | "cli" | "revoked";
}
export interface RunStart {
  type: "run.start";
  v: 1;
  id: MessageId;
}
export interface RunCancel {
  type: "run.cancel";
  v: 1;
  id: MessageId;
  run_id: RunId;
}
export interface AdMark {
  type: "ad.mark";
  v: 1;
  id: MessageId;
  ad_id: AdId;
  mark: Mark;
}
export interface AdUnmark {
  type: "ad.unmark";
  v: 1;
  id: MessageId;
  ad_id: AdId;
}
export interface AdShowAnyway {
  type: "ad.show_anyway";
  v: 1;
  id: MessageId;
  ad_id: AdId;
  on: boolean;
}
export interface ApplicationCreate {
  type: "application.create";
  v: 1;
  id: MessageId;
  company: string;
  title: string;
  note: Note;
}
export interface ApplicationUpdate {
  type: "application.update";
  v: 1;
  id: MessageId;
  application_id: ApplicationId;
  status?: ApplicationStatus;
  note?: Note;
}
export interface ApplicationRemove {
  type: "application.remove";
  v: 1;
  id: MessageId;
  application_id: ApplicationId;
  removed: boolean;
}
export interface ConfigUpdate {
  type: "config.update";
  v: 1;
  id: MessageId;
  patch: ConfigPatch;
}
export interface ConfigPatch {
  targets?: TargetsPatch;
  sources?: SourcesPatch;
  schedule?: SchedulePatch;
  notify?: NotifyPatch;
  model?: ModelConfigPatch;
}
export interface TargetsPatch {
  subfields?: RoleIds;
  levels?: Levels;
  years?: YearBands;
  hide_masters?: boolean;
  places?: Places;
  relocate?: boolean;
}
export interface SourcesPatch {
  nav?: boolean;
  employers?: boolean;
  finn?: boolean;
  linkedin?: boolean;
}
export interface SchedulePatch {
  times?: ClockTimes;
}
export interface NotifyPatch {
  website?: boolean;
  email?: EmailNotifyPatch;
}
export interface EmailNotifyPatch {
  on?: boolean;
  address?: Email | null;
}
export interface ModelConfigPatch {
  price?: PricePatch;
  monthly_limit_usd?: Money | null;
}
export interface PricePatch {
  input_per_mtok?: Money;
  output_per_mtok?: Money;
}
export interface ModelSelect {
  type: "model.select";
  v: 1;
  id: MessageId;
  provider: ProviderId;
  model: ModelId;
}
export interface KeyReplace {
  type: "key.replace";
  v: 1;
  id: MessageId;
  provider: ProviderId;
  key: string;
}
export interface CvUpload {
  type: "cv.upload";
  v: 1;
  id: MessageId;
  name: FileName;
  size_bytes: number;
  content_b64: string;
}
export interface ProfileUpdate {
  type: "profile.update";
  v: 1;
  id: MessageId;
  summary?: ShortText;
  education?: ShortText;
  languages?: Terms;
  stack?: Terms;
  projects?: ProjectEdit[];
}
export interface ProjectEdit {
  id: string | null;
  name: string;
}
export interface GuidanceUpdate {
  type: "guidance.update";
  v: 1;
  id: MessageId;
  text: string;
}
export interface GuidanceReset {
  type: "guidance.reset";
  v: 1;
  id: MessageId;
}
export interface ContextUpdate {
  type: "context.update";
  v: 1;
  id: MessageId;
  about: string;
}
export interface ContextUpload {
  type: "context.upload";
  v: 1;
  id: MessageId;
  name: FileName;
  content_b64: string;
}
export interface ContextRemove {
  type: "context.remove";
  v: 1;
  id: MessageId;
  doc_id: DocId;
}
export interface LearningForget {
  type: "learning.forget";
  v: 1;
  id: MessageId;
  decision_id: DecisionId;
}
export interface LearningForgetAll {
  type: "learning.forget_all";
  v: 1;
  id: MessageId;
}
export interface Unpair {
  type: "unpair";
  v: 1;
  id: MessageId;
}
export interface Chunk {
  type: "chunk";
  v: 1;
  chunk_id: MessageId;
  index: number;
  count: number;
  data: string;
}
