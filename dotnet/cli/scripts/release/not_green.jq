def state: if .status == "completed" then .conclusion else .status end;
def not_green: .status != "completed" or ([.conclusion] | inside(["success", "skipped", "neutral"]) | not);
($runs[0].workflow_runs | map(select(.event == "pull_request")) | group_by(.workflow_id) | map(max_by(.created_at))) as $latest
| ($latest | map({key: (.check_suite_id | tostring), value: .name}) | from_entries) as $names
| ($runs[0].workflow_runs | map(.check_suite_id)) as $suites
| ($checks[0].check_runs | map(select(not_green) | .check_suite.id)) as $not_green_suites
| ($latest[] | select(not_green) | select(.check_suite_id as $suite | $not_green_suites | index($suite) | not) | "\(.name) is \(state)"),
  ($checks[0].check_runs[]
    | .check_suite.id as $id
    | select($names[$id | tostring] != null or ($suites | index($id)) == null)
    | select(not_green)
    | "\($names[$id | tostring] // .app.slug) / \(.name) is \(state)")
