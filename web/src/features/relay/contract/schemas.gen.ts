export const payloadSchema = {
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "Payload",
  "type": "object",
  "oneOf": [
    {
      "$ref": "#/$defs/Status"
    },
    {
      "$ref": "#/$defs/AdsSnapshot"
    },
    {
      "$ref": "#/$defs/ApplicationsSnapshot"
    },
    {
      "$ref": "#/$defs/ConfigSnapshot"
    },
    {
      "$ref": "#/$defs/ProfileSnapshot"
    },
    {
      "$ref": "#/$defs/UsageSnapshot"
    },
    {
      "$ref": "#/$defs/ModelsSnapshot"
    },
    {
      "$ref": "#/$defs/GuidanceSnapshot"
    },
    {
      "$ref": "#/$defs/ContextSnapshot"
    },
    {
      "$ref": "#/$defs/LearningSnapshot"
    },
    {
      "$ref": "#/$defs/RunProgress"
    },
    {
      "$ref": "#/$defs/RunFinished"
    },
    {
      "$ref": "#/$defs/RunCancelled"
    },
    {
      "$ref": "#/$defs/Saved"
    },
    {
      "$ref": "#/$defs/ErrorMessage"
    },
    {
      "$ref": "#/$defs/Unpaired"
    },
    {
      "$ref": "#/$defs/RunStart"
    },
    {
      "$ref": "#/$defs/RunCancel"
    },
    {
      "$ref": "#/$defs/AdMark"
    },
    {
      "$ref": "#/$defs/AdUnmark"
    },
    {
      "$ref": "#/$defs/AdShowAnyway"
    },
    {
      "$ref": "#/$defs/ApplicationCreate"
    },
    {
      "$ref": "#/$defs/ApplicationUpdate"
    },
    {
      "$ref": "#/$defs/ApplicationRemove"
    },
    {
      "$ref": "#/$defs/ConfigUpdate"
    },
    {
      "$ref": "#/$defs/ModelSelect"
    },
    {
      "$ref": "#/$defs/KeyReplace"
    },
    {
      "$ref": "#/$defs/CvUpload"
    },
    {
      "$ref": "#/$defs/ProfileUpdate"
    },
    {
      "$ref": "#/$defs/GuidanceUpdate"
    },
    {
      "$ref": "#/$defs/GuidanceReset"
    },
    {
      "$ref": "#/$defs/ContextUpdate"
    },
    {
      "$ref": "#/$defs/ContextUpload"
    },
    {
      "$ref": "#/$defs/ContextRemove"
    },
    {
      "$ref": "#/$defs/LearningForget"
    },
    {
      "$ref": "#/$defs/LearningForgetAll"
    },
    {
      "$ref": "#/$defs/Unpair"
    },
    {
      "$ref": "#/$defs/Chunk"
    }
  ],
  "discriminator": {
    "propertyName": "type"
  },
  "$defs": {
    "MessageId": {
      "type": "string",
      "pattern": "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
      "not": {
        "pattern": "\\s"
      }
    },
    "RunId": {
      "type": "integer",
      "minimum": 1,
      "maximum": 9007199254740991
    },
    "AdId": {
      "type": "string",
      "minLength": 1,
      "maxLength": 200
    },
    "Company": {
      "type": "string",
      "minLength": 1,
      "maxLength": 200
    },
    "AdTitle": {
      "type": "string",
      "minLength": 1,
      "maxLength": 200
    },
    "ModelId": {
      "type": "string",
      "minLength": 1,
      "maxLength": 120
    },
    "Note": {
      "type": "string",
      "maxLength": 2000
    },
    "Terms": {
      "type": "array",
      "items": {
        "type": "string",
        "minLength": 1,
        "maxLength": 60
      },
      "maxItems": 40
    },
    "ApplicationId": {
      "type": "integer",
      "minimum": 1,
      "maximum": 9007199254740991
    },
    "DecisionId": {
      "type": "integer",
      "minimum": 1,
      "maximum": 9007199254740991
    },
    "Timestamp": {
      "type": "string",
      "pattern": "^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,9})?(Z|[+-]([01][0-9]|2[0-3]):[0-5][0-9])$",
      "not": {
        "pattern": "\\s"
      }
    },
    "Day": {
      "type": "string",
      "format": "date",
      "not": {
        "pattern": "^0000-"
      }
    },
    "ClockTime": {
      "type": "string",
      "pattern": "^([01][0-9]|2[0-3]):[0-5][0-9]$",
      "minLength": 5,
      "maxLength": 5
    },
    "Money": {
      "type": "number",
      "minimum": 0,
      "maximum": 1000000
    },
    "Count": {
      "type": "integer",
      "minimum": 0,
      "maximum": 9007199254740991
    },
    "Limit": {
      "type": "integer",
      "minimum": 1,
      "maximum": 9007199254740991
    },
    "ShortText": {
      "type": "string",
      "maxLength": 300
    },
    "RoleId": {
      "type": "string",
      "enum": [
        "backend",
        "frontend",
        "fullstack",
        "mobile",
        "data_engineering",
        "devops_platform",
        "ml_ai",
        "embedded",
        "game"
      ]
    },
    "RoleType": {
      "anyOf": [
        {
          "$ref": "#/$defs/RoleId"
        },
        {
          "const": "general_software"
        }
      ]
    },
    "SourceId": {
      "type": "string",
      "enum": [
        "nav",
        "finn",
        "linkedin",
        "teamtailor",
        "ashby"
      ]
    },
    "Fit": {
      "type": "string",
      "enum": [
        "strong",
        "partial",
        "on_request"
      ]
    },
    "RunMode": {
      "type": "string",
      "enum": [
        "scheduled",
        "manual",
        "dry_run"
      ]
    },
    "Mark": {
      "type": "string",
      "enum": [
        "applied",
        "skipped",
        "not_for_me"
      ]
    },
    "CheckState": {
      "type": "string",
      "enum": [
        "met",
        "partly",
        "missing"
      ]
    },
    "Check": {
      "type": "object",
      "required": [
        "text",
        "state"
      ],
      "properties": {
        "text": {
          "type": "string",
          "minLength": 1,
          "maxLength": 160
        },
        "state": {
          "$ref": "#/$defs/CheckState"
        }
      }
    },
    "Level": {
      "type": "string",
      "enum": [
        "junior",
        "mid",
        "senior",
        "lead",
        "none"
      ]
    },
    "YearBand": {
      "type": "string",
      "enum": [
        "0-1",
        "1-3",
        "3-6",
        "6+"
      ]
    },
    "RoleIds": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/RoleId"
      },
      "uniqueItems": true,
      "maxItems": 9
    },
    "Levels": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/Level"
      },
      "uniqueItems": true,
      "maxItems": 5
    },
    "YearBands": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/YearBand"
      },
      "uniqueItems": true,
      "maxItems": 4
    },
    "Places": {
      "type": "array",
      "items": {
        "type": "string",
        "minLength": 1,
        "maxLength": 80
      },
      "uniqueItems": true,
      "maxItems": 20
    },
    "ClockTimes": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/ClockTime"
      },
      "uniqueItems": true,
      "maxItems": 12
    },
    "ProviderId": {
      "type": "string",
      "enum": [
        "anthropic",
        "openai",
        "openrouter",
        "ollama"
      ]
    },
    "Language": {
      "type": "string",
      "enum": [
        "en",
        "nb"
      ]
    },
    "ApplicationStatus": {
      "type": "string",
      "enum": [
        "waiting",
        "interview",
        "offer",
        "accepted",
        "rejected"
      ]
    },
    "HiddenKind": {
      "type": "string",
      "enum": [
        "senior",
        "years",
        "masters",
        "role",
        "place",
        "other"
      ]
    },
    "ClosedReason": {
      "type": "string",
      "enum": [
        "deadline",
        "removed",
        "filled"
      ]
    },
    "ClosedMark": {
      "type": "string",
      "enum": [
        "applied",
        "skipped",
        "not_for_me",
        "none"
      ]
    },
    "Base64Data": {
      "type": "string",
      "pattern": "^[A-Za-z0-9+/]*={0,2}$",
      "not": {
        "pattern": "[^A-Za-z0-9+/=]"
      }
    },
    "DocId": {
      "type": "string",
      "pattern": "^doc_[a-z0-9_]+$",
      "maxLength": 64,
      "not": {
        "pattern": "[^a-z0-9_]"
      }
    },
    "Url": {
      "type": "string",
      "maxLength": 2048,
      "pattern": "^https?://[\\x21-\\x7e]+$",
      "not": {
        "pattern": "[^\\x21-\\x7e]|^https?://[^/?#\\\\]*\\\\[^/?#]*@"
      }
    },
    "Email": {
      "type": "string",
      "maxLength": 254,
      "pattern": "^[A-Za-z0-9!#$%&'*+/=?^_`{|}~.-]+@[A-Za-z0-9-]+(\\.[A-Za-z0-9-]+)*\\.[A-Za-z]+$",
      "not": {
        "pattern": "[^A-Za-z0-9!#$%&'*+/=?^_`{|}~.@-]"
      }
    },
    "FileName": {
      "type": "string",
      "minLength": 1,
      "maxLength": 120,
      "pattern": "\\.([pP][dD][fF]|[tT][xX][tT]|[mM][dD])$",
      "not": {
        "pattern": "[\\u0000-\\u001f\\u007f-\\u009f\\u00ad\\u034f\\u061c\\u115f-\\u1160\\u17b4-\\u17b5\\u180b-\\u180f\\u200b\\u200e-\\u200f\\u2028-\\u202e\\u2060-\\u206f\\u3164\\ufe00-\\ufe0f\\ufeff\\uffa0\\ufff0-\\ufff8\ud82f\udca0-\ud82f\udca3\ud834\udd73-\ud834\udd7a\udb40\udc00-\udb43\udfff/\\\\]"
      }
    },
    "Recheck": {
      "type": "object",
      "required": [
        "ads",
        "cost_usd"
      ],
      "properties": {
        "ads": {
          "$ref": "#/$defs/Count"
        },
        "cost_usd": {
          "anyOf": [
            {
              "$ref": "#/$defs/Money"
            },
            {
              "type": "null"
            }
          ]
        }
      }
    },
    "LastRun": {
      "type": "object",
      "required": [
        "run_id",
        "finished_at",
        "ads_read",
        "matches",
        "cost_usd"
      ],
      "properties": {
        "run_id": {
          "$ref": "#/$defs/RunId"
        },
        "finished_at": {
          "$ref": "#/$defs/Timestamp"
        },
        "ads_read": {
          "$ref": "#/$defs/Count"
        },
        "matches": {
          "$ref": "#/$defs/Count"
        },
        "cost_usd": {
          "$ref": "#/$defs/Money"
        }
      }
    },
    "ActiveRun": {
      "type": "object",
      "required": [
        "run_id",
        "mode",
        "started_at",
        "results_by"
      ],
      "properties": {
        "run_id": {
          "$ref": "#/$defs/RunId"
        },
        "mode": {
          "$ref": "#/$defs/RunMode"
        },
        "started_at": {
          "$ref": "#/$defs/Timestamp"
        },
        "results_by": {
          "anyOf": [
            {
              "$ref": "#/$defs/Timestamp"
            },
            {
              "type": "null"
            }
          ]
        }
      }
    },
    "ActiveAd": {
      "type": "object",
      "required": [
        "id",
        "company",
        "title",
        "place",
        "role_type",
        "source",
        "published_at",
        "found_at",
        "url",
        "deadline",
        "fit",
        "is_new",
        "today",
        "summary",
        "reason",
        "gaps",
        "checks",
        "mark",
        "marked_at",
        "shown_anyway"
      ],
      "properties": {
        "id": {
          "$ref": "#/$defs/AdId"
        },
        "company": {
          "$ref": "#/$defs/Company"
        },
        "title": {
          "$ref": "#/$defs/AdTitle"
        },
        "place": {
          "type": "string",
          "maxLength": 120
        },
        "role_type": {
          "$ref": "#/$defs/RoleType"
        },
        "source": {
          "$ref": "#/$defs/SourceId"
        },
        "published_at": {
          "anyOf": [
            {
              "$ref": "#/$defs/Timestamp"
            },
            {
              "type": "null"
            }
          ]
        },
        "found_at": {
          "$ref": "#/$defs/Timestamp"
        },
        "url": {
          "$ref": "#/$defs/Url"
        },
        "deadline": {
          "anyOf": [
            {
              "$ref": "#/$defs/Day"
            },
            {
              "type": "null"
            }
          ]
        },
        "fit": {
          "$ref": "#/$defs/Fit"
        },
        "is_new": {
          "type": "boolean"
        },
        "today": {
          "type": "boolean"
        },
        "summary": {
          "type": "string",
          "maxLength": 700
        },
        "reason": {
          "$ref": "#/$defs/ShortText"
        },
        "gaps": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/ShortText"
          },
          "maxItems": 5
        },
        "checks": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/Check"
          },
          "maxItems": 5
        },
        "mark": {
          "anyOf": [
            {
              "$ref": "#/$defs/Mark"
            },
            {
              "type": "null"
            }
          ]
        },
        "marked_at": {
          "anyOf": [
            {
              "$ref": "#/$defs/Timestamp"
            },
            {
              "type": "null"
            }
          ]
        },
        "shown_anyway": {
          "type": "boolean"
        }
      }
    },
    "HiddenAd": {
      "type": "object",
      "required": [
        "id",
        "company",
        "title",
        "place",
        "role_type",
        "source",
        "url",
        "found_at",
        "today",
        "kind",
        "reason"
      ],
      "properties": {
        "id": {
          "$ref": "#/$defs/AdId"
        },
        "company": {
          "$ref": "#/$defs/Company"
        },
        "title": {
          "$ref": "#/$defs/AdTitle"
        },
        "place": {
          "type": "string",
          "maxLength": 120
        },
        "role_type": {
          "anyOf": [
            {
              "$ref": "#/$defs/RoleType"
            },
            {
              "type": "null"
            }
          ]
        },
        "source": {
          "$ref": "#/$defs/SourceId"
        },
        "url": {
          "$ref": "#/$defs/Url"
        },
        "found_at": {
          "$ref": "#/$defs/Timestamp"
        },
        "today": {
          "type": "boolean"
        },
        "kind": {
          "$ref": "#/$defs/HiddenKind"
        },
        "reason": {
          "type": "string",
          "minLength": 1,
          "maxLength": 120
        }
      }
    },
    "HiddenCounts": {
      "type": "object",
      "required": [
        "senior",
        "years",
        "masters",
        "role",
        "place",
        "other"
      ],
      "properties": {
        "senior": {
          "$ref": "#/$defs/Count"
        },
        "years": {
          "$ref": "#/$defs/Count"
        },
        "masters": {
          "$ref": "#/$defs/Count"
        },
        "role": {
          "$ref": "#/$defs/Count"
        },
        "place": {
          "$ref": "#/$defs/Count"
        },
        "other": {
          "$ref": "#/$defs/Count"
        }
      }
    },
    "ClosedAd": {
      "type": "object",
      "required": [
        "id",
        "closed_on",
        "company",
        "title",
        "source",
        "reason",
        "mark"
      ],
      "properties": {
        "id": {
          "$ref": "#/$defs/AdId"
        },
        "closed_on": {
          "$ref": "#/$defs/Day"
        },
        "company": {
          "$ref": "#/$defs/Company"
        },
        "title": {
          "$ref": "#/$defs/AdTitle"
        },
        "source": {
          "$ref": "#/$defs/SourceId"
        },
        "reason": {
          "$ref": "#/$defs/ClosedReason"
        },
        "mark": {
          "$ref": "#/$defs/ClosedMark"
        }
      }
    },
    "HistoryEntry": {
      "type": "object",
      "required": [
        "at",
        "text"
      ],
      "properties": {
        "at": {
          "$ref": "#/$defs/Timestamp"
        },
        "text": {
          "type": "string",
          "minLength": 1,
          "maxLength": 200
        }
      }
    },
    "Application": {
      "type": "object",
      "required": [
        "id",
        "ad_id",
        "company",
        "title",
        "place",
        "role_type",
        "source",
        "url",
        "deadline",
        "fit",
        "status",
        "note",
        "applied_at",
        "found_at",
        "cv_name",
        "checks",
        "history",
        "removed"
      ],
      "properties": {
        "id": {
          "$ref": "#/$defs/ApplicationId"
        },
        "ad_id": {
          "anyOf": [
            {
              "$ref": "#/$defs/AdId"
            },
            {
              "type": "null"
            }
          ]
        },
        "company": {
          "$ref": "#/$defs/Company"
        },
        "title": {
          "type": "string",
          "maxLength": 200
        },
        "place": {
          "type": "string",
          "maxLength": 120
        },
        "role_type": {
          "anyOf": [
            {
              "$ref": "#/$defs/RoleType"
            },
            {
              "type": "null"
            }
          ]
        },
        "source": {
          "anyOf": [
            {
              "$ref": "#/$defs/SourceId"
            },
            {
              "type": "null"
            }
          ]
        },
        "url": {
          "anyOf": [
            {
              "$ref": "#/$defs/Url"
            },
            {
              "type": "null"
            }
          ]
        },
        "deadline": {
          "anyOf": [
            {
              "$ref": "#/$defs/Day"
            },
            {
              "type": "null"
            }
          ]
        },
        "fit": {
          "anyOf": [
            {
              "$ref": "#/$defs/Fit"
            },
            {
              "type": "null"
            }
          ]
        },
        "status": {
          "$ref": "#/$defs/ApplicationStatus"
        },
        "note": {
          "$ref": "#/$defs/Note"
        },
        "applied_at": {
          "$ref": "#/$defs/Timestamp"
        },
        "found_at": {
          "anyOf": [
            {
              "$ref": "#/$defs/Timestamp"
            },
            {
              "type": "null"
            }
          ]
        },
        "cv_name": {
          "anyOf": [
            {
              "type": "string",
              "maxLength": 120
            },
            {
              "type": "null"
            }
          ]
        },
        "checks": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/Check"
          },
          "maxItems": 5
        },
        "history": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/HistoryEntry"
          },
          "maxItems": 20
        },
        "removed": {
          "type": "boolean"
        }
      }
    },
    "RoleOption": {
      "type": "object",
      "required": [
        "id",
        "label"
      ],
      "properties": {
        "id": {
          "$ref": "#/$defs/RoleId"
        },
        "label": {
          "type": "string",
          "minLength": 1,
          "maxLength": 40
        }
      }
    },
    "Targets": {
      "type": "object",
      "required": [
        "subfields",
        "levels",
        "years",
        "hide_masters",
        "places",
        "relocate"
      ],
      "properties": {
        "subfields": {
          "$ref": "#/$defs/RoleIds"
        },
        "levels": {
          "$ref": "#/$defs/Levels"
        },
        "years": {
          "$ref": "#/$defs/YearBands"
        },
        "hide_masters": {
          "type": "boolean"
        },
        "places": {
          "$ref": "#/$defs/Places"
        },
        "relocate": {
          "type": "boolean"
        }
      }
    },
    "Sources": {
      "type": "object",
      "required": [
        "nav",
        "employers",
        "finn",
        "linkedin"
      ],
      "properties": {
        "nav": {
          "type": "boolean"
        },
        "employers": {
          "type": "boolean"
        },
        "finn": {
          "type": "boolean"
        },
        "linkedin": {
          "type": "boolean"
        }
      }
    },
    "Schedule": {
      "type": "object",
      "required": [
        "times",
        "min_interval_hours"
      ],
      "properties": {
        "times": {
          "$ref": "#/$defs/ClockTimes"
        },
        "min_interval_hours": {
          "type": "integer",
          "const": 2
        }
      }
    },
    "EmailNotify": {
      "type": "object",
      "required": [
        "on",
        "address"
      ],
      "properties": {
        "on": {
          "type": "boolean"
        },
        "address": {
          "anyOf": [
            {
              "$ref": "#/$defs/Email"
            },
            {
              "type": "null"
            }
          ]
        }
      }
    },
    "Notify": {
      "type": "object",
      "required": [
        "website",
        "email"
      ],
      "properties": {
        "website": {
          "type": "boolean"
        },
        "email": {
          "$ref": "#/$defs/EmailNotify"
        }
      }
    },
    "Price": {
      "type": "object",
      "required": [
        "input_per_mtok",
        "output_per_mtok"
      ],
      "properties": {
        "input_per_mtok": {
          "$ref": "#/$defs/Money"
        },
        "output_per_mtok": {
          "$ref": "#/$defs/Money"
        }
      }
    },
    "ModelConfig": {
      "type": "object",
      "required": [
        "provider",
        "model",
        "key_hint",
        "price",
        "monthly_limit_usd"
      ],
      "properties": {
        "provider": {
          "anyOf": [
            {
              "$ref": "#/$defs/ProviderId"
            },
            {
              "type": "null"
            }
          ]
        },
        "model": {
          "anyOf": [
            {
              "$ref": "#/$defs/ModelId"
            },
            {
              "type": "null"
            }
          ]
        },
        "key_hint": {
          "anyOf": [
            {
              "type": "string",
              "maxLength": 4
            },
            {
              "type": "null"
            }
          ]
        },
        "price": {
          "$ref": "#/$defs/Price"
        },
        "monthly_limit_usd": {
          "anyOf": [
            {
              "$ref": "#/$defs/Money"
            },
            {
              "type": "null"
            }
          ]
        }
      }
    },
    "TargetsPatch": {
      "type": "object",
      "properties": {
        "subfields": {
          "$ref": "#/$defs/RoleIds"
        },
        "levels": {
          "$ref": "#/$defs/Levels"
        },
        "years": {
          "$ref": "#/$defs/YearBands"
        },
        "hide_masters": {
          "type": "boolean"
        },
        "places": {
          "$ref": "#/$defs/Places"
        },
        "relocate": {
          "type": "boolean"
        }
      }
    },
    "SourcesPatch": {
      "type": "object",
      "properties": {
        "nav": {
          "type": "boolean"
        },
        "employers": {
          "type": "boolean"
        },
        "finn": {
          "type": "boolean"
        },
        "linkedin": {
          "type": "boolean"
        }
      }
    },
    "SchedulePatch": {
      "type": "object",
      "properties": {
        "times": {
          "$ref": "#/$defs/ClockTimes"
        }
      }
    },
    "EmailNotifyPatch": {
      "type": "object",
      "properties": {
        "on": {
          "type": "boolean"
        },
        "address": {
          "anyOf": [
            {
              "$ref": "#/$defs/Email"
            },
            {
              "type": "null"
            }
          ]
        }
      }
    },
    "NotifyPatch": {
      "type": "object",
      "properties": {
        "website": {
          "type": "boolean"
        },
        "email": {
          "$ref": "#/$defs/EmailNotifyPatch"
        }
      }
    },
    "PricePatch": {
      "type": "object",
      "properties": {
        "input_per_mtok": {
          "$ref": "#/$defs/Money"
        },
        "output_per_mtok": {
          "$ref": "#/$defs/Money"
        }
      }
    },
    "ModelConfigPatch": {
      "type": "object",
      "properties": {
        "price": {
          "$ref": "#/$defs/PricePatch"
        },
        "monthly_limit_usd": {
          "anyOf": [
            {
              "$ref": "#/$defs/Money"
            },
            {
              "type": "null"
            }
          ]
        }
      }
    },
    "ConfigPatch": {
      "type": "object",
      "properties": {
        "targets": {
          "$ref": "#/$defs/TargetsPatch"
        },
        "sources": {
          "$ref": "#/$defs/SourcesPatch"
        },
        "schedule": {
          "$ref": "#/$defs/SchedulePatch"
        },
        "notify": {
          "$ref": "#/$defs/NotifyPatch"
        },
        "model": {
          "$ref": "#/$defs/ModelConfigPatch"
        },
        "language": {
          "$ref": "#/$defs/Language"
        }
      }
    },
    "ProjectRef": {
      "type": "object",
      "required": [
        "id",
        "name"
      ],
      "properties": {
        "id": {
          "type": "string",
          "minLength": 1,
          "maxLength": 40
        },
        "name": {
          "type": "string",
          "minLength": 1,
          "maxLength": 120
        }
      }
    },
    "ProjectEdit": {
      "type": "object",
      "required": [
        "id",
        "name"
      ],
      "properties": {
        "id": {
          "anyOf": [
            {
              "type": "string",
              "minLength": 1,
              "maxLength": 40
            },
            {
              "type": "null"
            }
          ]
        },
        "name": {
          "type": "string",
          "minLength": 1,
          "maxLength": 120
        }
      }
    },
    "UsageDay": {
      "type": "object",
      "required": [
        "day",
        "calls",
        "input_tokens",
        "output_tokens",
        "cost_usd",
        "ads_read",
        "ads_checked"
      ],
      "properties": {
        "day": {
          "$ref": "#/$defs/Day"
        },
        "calls": {
          "$ref": "#/$defs/Count"
        },
        "input_tokens": {
          "$ref": "#/$defs/Count"
        },
        "output_tokens": {
          "$ref": "#/$defs/Count"
        },
        "cost_usd": {
          "$ref": "#/$defs/Money"
        },
        "ads_read": {
          "$ref": "#/$defs/Count"
        },
        "ads_checked": {
          "$ref": "#/$defs/Count"
        }
      }
    },
    "ModelInfo": {
      "type": "object",
      "required": [
        "id",
        "name",
        "input_per_mtok",
        "output_per_mtok",
        "usd_per_day"
      ],
      "properties": {
        "id": {
          "$ref": "#/$defs/ModelId"
        },
        "name": {
          "type": "string",
          "minLength": 1,
          "maxLength": 120
        },
        "input_per_mtok": {
          "anyOf": [
            {
              "$ref": "#/$defs/Money"
            },
            {
              "type": "null"
            }
          ]
        },
        "output_per_mtok": {
          "anyOf": [
            {
              "$ref": "#/$defs/Money"
            },
            {
              "type": "null"
            }
          ]
        },
        "usd_per_day": {
          "anyOf": [
            {
              "$ref": "#/$defs/Money"
            },
            {
              "type": "null"
            }
          ]
        }
      }
    },
    "ProviderModels": {
      "type": "object",
      "required": [
        "provider",
        "error",
        "models"
      ],
      "properties": {
        "provider": {
          "$ref": "#/$defs/ProviderId"
        },
        "error": {
          "anyOf": [
            {
              "$ref": "#/$defs/ShortText"
            },
            {
              "type": "null"
            }
          ]
        },
        "models": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/ModelInfo"
          },
          "maxItems": 1000
        }
      }
    },
    "ContextDoc": {
      "type": "object",
      "required": [
        "id",
        "name",
        "chars",
        "added_at"
      ],
      "properties": {
        "id": {
          "$ref": "#/$defs/DocId"
        },
        "name": {
          "type": "string",
          "minLength": 1,
          "maxLength": 120
        },
        "chars": {
          "$ref": "#/$defs/Count"
        },
        "added_at": {
          "$ref": "#/$defs/Day"
        }
      }
    },
    "LearningExample": {
      "type": "object",
      "required": [
        "decision_id",
        "ad_id",
        "company",
        "title",
        "decision",
        "at"
      ],
      "properties": {
        "decision_id": {
          "$ref": "#/$defs/DecisionId"
        },
        "ad_id": {
          "$ref": "#/$defs/AdId"
        },
        "company": {
          "$ref": "#/$defs/Company"
        },
        "title": {
          "$ref": "#/$defs/AdTitle"
        },
        "decision": {
          "type": "string",
          "enum": [
            "applied",
            "not_for_me",
            "show_anyway"
          ]
        },
        "at": {
          "$ref": "#/$defs/Timestamp"
        }
      }
    },
    "SourceStep": {
      "type": "object",
      "required": [
        "source",
        "state",
        "found_new"
      ],
      "properties": {
        "source": {
          "$ref": "#/$defs/SourceId"
        },
        "state": {
          "type": "string",
          "enum": [
            "pending",
            "active",
            "done",
            "failed"
          ]
        },
        "found_new": {
          "anyOf": [
            {
              "$ref": "#/$defs/Count"
            },
            {
              "type": "null"
            }
          ]
        }
      }
    },
    "ModelStep": {
      "type": "object",
      "required": [
        "state",
        "checked",
        "total",
        "results_by"
      ],
      "properties": {
        "state": {
          "type": "string",
          "enum": [
            "pending",
            "active",
            "done",
            "skipped"
          ]
        },
        "checked": {
          "$ref": "#/$defs/Count"
        },
        "total": {
          "$ref": "#/$defs/Count"
        },
        "results_by": {
          "anyOf": [
            {
              "$ref": "#/$defs/Timestamp"
            },
            {
              "type": "null"
            }
          ]
        }
      }
    },
    "Status": {
      "type": "object",
      "required": [
        "type",
        "v",
        "cli_version",
        "contract_version",
        "machine",
        "paired_at",
        "last_run",
        "next_run_at",
        "running",
        "budget_paused"
      ],
      "properties": {
        "type": {
          "const": "status"
        },
        "v": {
          "const": 1
        },
        "cli_version": {
          "type": "string",
          "minLength": 1,
          "maxLength": 32
        },
        "contract_version": {
          "type": "integer",
          "minimum": 1,
          "maximum": 9007199254740991
        },
        "machine": {
          "type": "string",
          "minLength": 1,
          "maxLength": 64
        },
        "paired_at": {
          "$ref": "#/$defs/Timestamp"
        },
        "last_run": {
          "anyOf": [
            {
              "$ref": "#/$defs/LastRun"
            },
            {
              "type": "null"
            }
          ]
        },
        "next_run_at": {
          "anyOf": [
            {
              "$ref": "#/$defs/Timestamp"
            },
            {
              "type": "null"
            }
          ]
        },
        "running": {
          "anyOf": [
            {
              "$ref": "#/$defs/ActiveRun"
            },
            {
              "type": "null"
            }
          ]
        },
        "budget_paused": {
          "type": "boolean"
        }
      }
    },
    "AdsSnapshot": {
      "type": "object",
      "required": [
        "type",
        "v",
        "generated_at",
        "as_of",
        "active",
        "active_total",
        "hidden",
        "hidden_total",
        "hidden_counts",
        "closed",
        "closed_total"
      ],
      "properties": {
        "type": {
          "const": "ads.snapshot"
        },
        "v": {
          "const": 1
        },
        "generated_at": {
          "$ref": "#/$defs/Timestamp"
        },
        "as_of": {
          "$ref": "#/$defs/Day"
        },
        "active": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/ActiveAd"
          },
          "maxItems": 73
        },
        "active_total": {
          "$ref": "#/$defs/Count"
        },
        "hidden": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/HiddenAd"
          },
          "maxItems": 500
        },
        "hidden_total": {
          "$ref": "#/$defs/Count"
        },
        "hidden_counts": {
          "$ref": "#/$defs/HiddenCounts"
        },
        "closed": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/ClosedAd"
          },
          "maxItems": 200
        },
        "closed_total": {
          "$ref": "#/$defs/Count"
        }
      }
    },
    "ApplicationsSnapshot": {
      "type": "object",
      "required": [
        "type",
        "v",
        "generated_at",
        "applications",
        "applications_total"
      ],
      "properties": {
        "type": {
          "const": "applications.snapshot"
        },
        "v": {
          "const": 1
        },
        "generated_at": {
          "$ref": "#/$defs/Timestamp"
        },
        "applications": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/Application"
          },
          "maxItems": 145
        },
        "applications_total": {
          "$ref": "#/$defs/Count"
        }
      }
    },
    "ConfigSnapshot": {
      "type": "object",
      "required": [
        "type",
        "v",
        "roles",
        "targets",
        "sources",
        "schedule",
        "notify",
        "model",
        "language"
      ],
      "properties": {
        "type": {
          "const": "config.snapshot"
        },
        "v": {
          "const": 1
        },
        "roles": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/RoleOption"
          },
          "maxItems": 9
        },
        "targets": {
          "$ref": "#/$defs/Targets"
        },
        "sources": {
          "$ref": "#/$defs/Sources"
        },
        "schedule": {
          "$ref": "#/$defs/Schedule"
        },
        "notify": {
          "$ref": "#/$defs/Notify"
        },
        "model": {
          "$ref": "#/$defs/ModelConfig"
        },
        "language": {
          "$ref": "#/$defs/Language"
        }
      }
    },
    "ProfileSnapshot": {
      "type": "object",
      "required": [
        "type",
        "v",
        "cv_name",
        "cv_read_at",
        "summary",
        "education",
        "languages",
        "stack",
        "projects"
      ],
      "properties": {
        "type": {
          "const": "profile.snapshot"
        },
        "v": {
          "const": 1
        },
        "cv_name": {
          "anyOf": [
            {
              "type": "string",
              "maxLength": 120
            },
            {
              "type": "null"
            }
          ]
        },
        "cv_read_at": {
          "anyOf": [
            {
              "$ref": "#/$defs/Timestamp"
            },
            {
              "type": "null"
            }
          ]
        },
        "summary": {
          "$ref": "#/$defs/ShortText"
        },
        "education": {
          "$ref": "#/$defs/ShortText"
        },
        "languages": {
          "$ref": "#/$defs/Terms"
        },
        "stack": {
          "$ref": "#/$defs/Terms"
        },
        "projects": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/ProjectRef"
          },
          "maxItems": 20
        }
      }
    },
    "UsageSnapshot": {
      "type": "object",
      "required": [
        "type",
        "v",
        "month",
        "month_tokens",
        "today_tokens",
        "month_usd",
        "today_usd",
        "resets_on",
        "days"
      ],
      "properties": {
        "type": {
          "const": "usage.snapshot"
        },
        "v": {
          "const": 1
        },
        "month": {
          "type": "string",
          "pattern": "^[0-9]{4}-(0[1-9]|1[0-2])$",
          "not": {
            "pattern": "^0000-"
          },
          "minLength": 7,
          "maxLength": 7
        },
        "month_tokens": {
          "$ref": "#/$defs/Count"
        },
        "today_tokens": {
          "$ref": "#/$defs/Count"
        },
        "month_usd": {
          "$ref": "#/$defs/Money"
        },
        "today_usd": {
          "$ref": "#/$defs/Money"
        },
        "resets_on": {
          "$ref": "#/$defs/Day"
        },
        "days": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/UsageDay"
          },
          "maxItems": 14
        }
      }
    },
    "ModelsSnapshot": {
      "type": "object",
      "required": [
        "type",
        "v",
        "providers"
      ],
      "properties": {
        "type": {
          "const": "models.snapshot"
        },
        "v": {
          "const": 1
        },
        "providers": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/ProviderModels"
          },
          "maxItems": 4
        }
      }
    },
    "GuidanceSnapshot": {
      "type": "object",
      "required": [
        "type",
        "v",
        "text",
        "is_default",
        "limit"
      ],
      "properties": {
        "type": {
          "const": "guidance.snapshot"
        },
        "v": {
          "const": 1
        },
        "text": {
          "type": "string",
          "maxLength": 12000
        },
        "is_default": {
          "type": "boolean"
        },
        "limit": {
          "$ref": "#/$defs/Limit"
        }
      }
    },
    "ContextSnapshot": {
      "type": "object",
      "required": [
        "type",
        "v",
        "about",
        "about_limit",
        "docs",
        "docs_limit"
      ],
      "properties": {
        "type": {
          "const": "context.snapshot"
        },
        "v": {
          "const": 1
        },
        "about": {
          "type": "string",
          "maxLength": 4000
        },
        "about_limit": {
          "$ref": "#/$defs/Limit"
        },
        "docs": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/ContextDoc"
          },
          "maxItems": 10
        },
        "docs_limit": {
          "$ref": "#/$defs/Limit"
        }
      }
    },
    "LearningSnapshot": {
      "type": "object",
      "required": [
        "type",
        "v",
        "state",
        "unavailable_reason",
        "decision_count",
        "cold_start_threshold",
        "examples",
        "examples_total"
      ],
      "properties": {
        "type": {
          "const": "learning.snapshot"
        },
        "v": {
          "const": 1
        },
        "state": {
          "type": "string",
          "enum": [
            "unavailable",
            "collecting",
            "preparing",
            "on"
          ]
        },
        "unavailable_reason": {
          "anyOf": [
            {
              "type": "string",
              "maxLength": 200
            },
            {
              "type": "null"
            }
          ]
        },
        "decision_count": {
          "$ref": "#/$defs/Count"
        },
        "cold_start_threshold": {
          "type": "integer",
          "const": 20
        },
        "examples": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/LearningExample"
          },
          "maxItems": 200
        },
        "examples_total": {
          "$ref": "#/$defs/Count"
        }
      }
    },
    "RunProgress": {
      "type": "object",
      "required": [
        "type",
        "v",
        "run_id",
        "mode",
        "started_at",
        "percent",
        "sources",
        "model"
      ],
      "properties": {
        "type": {
          "const": "run.progress"
        },
        "v": {
          "const": 1
        },
        "run_id": {
          "$ref": "#/$defs/RunId"
        },
        "mode": {
          "$ref": "#/$defs/RunMode"
        },
        "started_at": {
          "$ref": "#/$defs/Timestamp"
        },
        "percent": {
          "type": "integer",
          "minimum": 0,
          "maximum": 100
        },
        "sources": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/SourceStep"
          },
          "maxItems": 5
        },
        "model": {
          "$ref": "#/$defs/ModelStep"
        }
      }
    },
    "RunFinished": {
      "type": "object",
      "required": [
        "type",
        "v",
        "run_id",
        "finished_at",
        "ads_read",
        "new_ads",
        "matches",
        "hidden",
        "cost_usd",
        "failed_sources",
        "budget_paused"
      ],
      "properties": {
        "type": {
          "const": "run.finished"
        },
        "v": {
          "const": 1
        },
        "run_id": {
          "$ref": "#/$defs/RunId"
        },
        "finished_at": {
          "$ref": "#/$defs/Timestamp"
        },
        "ads_read": {
          "$ref": "#/$defs/Count"
        },
        "new_ads": {
          "$ref": "#/$defs/Count"
        },
        "matches": {
          "$ref": "#/$defs/Count"
        },
        "hidden": {
          "$ref": "#/$defs/Count"
        },
        "cost_usd": {
          "$ref": "#/$defs/Money"
        },
        "failed_sources": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/SourceId"
          },
          "maxItems": 5
        },
        "budget_paused": {
          "type": "boolean"
        }
      }
    },
    "RunCancelled": {
      "type": "object",
      "required": [
        "type",
        "v",
        "run_id",
        "at"
      ],
      "properties": {
        "type": {
          "const": "run.cancelled"
        },
        "v": {
          "const": 1
        },
        "run_id": {
          "$ref": "#/$defs/RunId"
        },
        "at": {
          "$ref": "#/$defs/Timestamp"
        }
      }
    },
    "Saved": {
      "type": "object",
      "required": [
        "type",
        "v",
        "ref",
        "applies_from"
      ],
      "properties": {
        "type": {
          "const": "saved"
        },
        "v": {
          "const": 1
        },
        "ref": {
          "$ref": "#/$defs/MessageId"
        },
        "applies_from": {
          "type": "string",
          "enum": [
            "now",
            "next_run"
          ]
        },
        "recheck": {
          "$ref": "#/$defs/Recheck"
        }
      }
    },
    "ErrorMessage": {
      "type": "object",
      "required": [
        "type",
        "v",
        "ref",
        "code",
        "message",
        "field"
      ],
      "properties": {
        "type": {
          "const": "error"
        },
        "v": {
          "const": 1
        },
        "ref": {
          "anyOf": [
            {
              "$ref": "#/$defs/MessageId"
            },
            {
              "type": "null"
            }
          ]
        },
        "code": {
          "type": "string",
          "enum": [
            "invalid_payload",
            "unknown_type",
            "unsupported_version",
            "not_found",
            "run_in_progress",
            "no_run",
            "run_failed",
            "key_rejected",
            "cv_type",
            "cv_unreadable",
            "cv_no_text",
            "cv_too_large",
            "cv_parse_failed",
            "chunk_incomplete",
            "internal",
            "guidance_empty",
            "context_full",
            "context_unreadable"
          ]
        },
        "message": {
          "type": "string",
          "minLength": 1,
          "maxLength": 300
        },
        "field": {
          "anyOf": [
            {
              "type": "string",
              "maxLength": 120
            },
            {
              "type": "null"
            }
          ]
        }
      }
    },
    "Unpaired": {
      "type": "object",
      "required": [
        "type",
        "v",
        "reason"
      ],
      "properties": {
        "type": {
          "const": "unpaired"
        },
        "v": {
          "const": 1
        },
        "reason": {
          "type": "string",
          "enum": [
            "refused",
            "web",
            "cli",
            "revoked"
          ]
        }
      }
    },
    "RunStart": {
      "type": "object",
      "required": [
        "type",
        "v",
        "id"
      ],
      "properties": {
        "type": {
          "const": "run.start"
        },
        "v": {
          "const": 1
        },
        "id": {
          "$ref": "#/$defs/MessageId"
        }
      }
    },
    "RunCancel": {
      "type": "object",
      "required": [
        "type",
        "v",
        "id",
        "run_id"
      ],
      "properties": {
        "type": {
          "const": "run.cancel"
        },
        "v": {
          "const": 1
        },
        "id": {
          "$ref": "#/$defs/MessageId"
        },
        "run_id": {
          "$ref": "#/$defs/RunId"
        }
      }
    },
    "AdMark": {
      "type": "object",
      "required": [
        "type",
        "v",
        "id",
        "ad_id",
        "mark"
      ],
      "properties": {
        "type": {
          "const": "ad.mark"
        },
        "v": {
          "const": 1
        },
        "id": {
          "$ref": "#/$defs/MessageId"
        },
        "ad_id": {
          "$ref": "#/$defs/AdId"
        },
        "mark": {
          "$ref": "#/$defs/Mark"
        }
      }
    },
    "AdUnmark": {
      "type": "object",
      "required": [
        "type",
        "v",
        "id",
        "ad_id"
      ],
      "properties": {
        "type": {
          "const": "ad.unmark"
        },
        "v": {
          "const": 1
        },
        "id": {
          "$ref": "#/$defs/MessageId"
        },
        "ad_id": {
          "$ref": "#/$defs/AdId"
        }
      }
    },
    "AdShowAnyway": {
      "type": "object",
      "required": [
        "type",
        "v",
        "id",
        "ad_id",
        "on"
      ],
      "properties": {
        "type": {
          "const": "ad.show_anyway"
        },
        "v": {
          "const": 1
        },
        "id": {
          "$ref": "#/$defs/MessageId"
        },
        "ad_id": {
          "$ref": "#/$defs/AdId"
        },
        "on": {
          "type": "boolean"
        }
      }
    },
    "ApplicationCreate": {
      "type": "object",
      "required": [
        "type",
        "v",
        "id",
        "company",
        "title",
        "note"
      ],
      "properties": {
        "type": {
          "const": "application.create"
        },
        "v": {
          "const": 1
        },
        "id": {
          "$ref": "#/$defs/MessageId"
        },
        "company": {
          "$ref": "#/$defs/Company",
          "type": "string",
          "pattern": "[^\\u0000-\\u0020\\u007f-\\u00a0\\u00ad\\u034f\\u061c\\u115f-\\u1160\\u1680\\u17b4-\\u17b5\\u180b-\\u180f\\u2000-\\u200f\\u2028-\\u202f\\u205f-\\u206f\\u2800\\u3000\\u3164\\ufe00-\\ufe0f\\ufeff\\uffa0\\ufff0-\\ufff8\ud82f\udca0-\ud82f\udca3\ud834\udd73-\ud834\udd7a\udb40\udc00-\udb43\udfff]"
        },
        "title": {
          "type": "string",
          "maxLength": 200
        },
        "note": {
          "$ref": "#/$defs/Note"
        }
      }
    },
    "ApplicationUpdate": {
      "type": "object",
      "required": [
        "type",
        "v",
        "id",
        "application_id"
      ],
      "properties": {
        "type": {
          "const": "application.update"
        },
        "v": {
          "const": 1
        },
        "id": {
          "$ref": "#/$defs/MessageId"
        },
        "application_id": {
          "$ref": "#/$defs/ApplicationId"
        },
        "status": {
          "$ref": "#/$defs/ApplicationStatus"
        },
        "note": {
          "$ref": "#/$defs/Note"
        }
      }
    },
    "ApplicationRemove": {
      "type": "object",
      "required": [
        "type",
        "v",
        "id",
        "application_id",
        "removed"
      ],
      "properties": {
        "type": {
          "const": "application.remove"
        },
        "v": {
          "const": 1
        },
        "id": {
          "$ref": "#/$defs/MessageId"
        },
        "application_id": {
          "$ref": "#/$defs/ApplicationId"
        },
        "removed": {
          "type": "boolean"
        }
      }
    },
    "ConfigUpdate": {
      "type": "object",
      "required": [
        "type",
        "v",
        "id",
        "patch"
      ],
      "properties": {
        "type": {
          "const": "config.update"
        },
        "v": {
          "const": 1
        },
        "id": {
          "$ref": "#/$defs/MessageId"
        },
        "patch": {
          "$ref": "#/$defs/ConfigPatch"
        }
      }
    },
    "ModelSelect": {
      "type": "object",
      "required": [
        "type",
        "v",
        "id",
        "provider",
        "model"
      ],
      "properties": {
        "type": {
          "const": "model.select"
        },
        "v": {
          "const": 1
        },
        "id": {
          "$ref": "#/$defs/MessageId"
        },
        "provider": {
          "$ref": "#/$defs/ProviderId"
        },
        "model": {
          "$ref": "#/$defs/ModelId"
        }
      }
    },
    "KeyReplace": {
      "type": "object",
      "required": [
        "type",
        "v",
        "id",
        "provider",
        "key"
      ],
      "properties": {
        "type": {
          "const": "key.replace"
        },
        "v": {
          "const": 1
        },
        "id": {
          "$ref": "#/$defs/MessageId"
        },
        "provider": {
          "$ref": "#/$defs/ProviderId"
        },
        "key": {
          "type": "string",
          "pattern": "^[\\x21-\\x7e]{12,512}$",
          "not": {
            "pattern": "[^\\x21-\\x7e]"
          }
        }
      }
    },
    "CvUpload": {
      "type": "object",
      "required": [
        "type",
        "v",
        "id",
        "name",
        "size_bytes",
        "content_b64"
      ],
      "properties": {
        "type": {
          "const": "cv.upload"
        },
        "v": {
          "const": 1
        },
        "id": {
          "$ref": "#/$defs/MessageId"
        },
        "name": {
          "$ref": "#/$defs/FileName"
        },
        "size_bytes": {
          "type": "integer",
          "minimum": 1,
          "maximum": 5242880
        },
        "content_b64": {
          "$ref": "#/$defs/Base64Data",
          "type": "string",
          "maxLength": 6990508
        }
      }
    },
    "ProfileUpdate": {
      "type": "object",
      "required": [
        "type",
        "v",
        "id"
      ],
      "properties": {
        "type": {
          "const": "profile.update"
        },
        "v": {
          "const": 1
        },
        "id": {
          "$ref": "#/$defs/MessageId"
        },
        "summary": {
          "$ref": "#/$defs/ShortText"
        },
        "education": {
          "$ref": "#/$defs/ShortText"
        },
        "languages": {
          "$ref": "#/$defs/Terms"
        },
        "stack": {
          "$ref": "#/$defs/Terms"
        },
        "projects": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/ProjectEdit"
          },
          "maxItems": 20
        }
      }
    },
    "GuidanceUpdate": {
      "type": "object",
      "required": [
        "type",
        "v",
        "id",
        "text"
      ],
      "properties": {
        "type": {
          "const": "guidance.update"
        },
        "v": {
          "const": 1
        },
        "id": {
          "$ref": "#/$defs/MessageId"
        },
        "text": {
          "type": "string",
          "maxLength": 12000
        }
      }
    },
    "GuidanceReset": {
      "type": "object",
      "required": [
        "type",
        "v",
        "id"
      ],
      "properties": {
        "type": {
          "const": "guidance.reset"
        },
        "v": {
          "const": 1
        },
        "id": {
          "$ref": "#/$defs/MessageId"
        }
      }
    },
    "ContextUpdate": {
      "type": "object",
      "required": [
        "type",
        "v",
        "id",
        "about"
      ],
      "properties": {
        "type": {
          "const": "context.update"
        },
        "v": {
          "const": 1
        },
        "id": {
          "$ref": "#/$defs/MessageId"
        },
        "about": {
          "type": "string",
          "maxLength": 4000
        }
      }
    },
    "ContextUpload": {
      "type": "object",
      "required": [
        "type",
        "v",
        "id",
        "name",
        "content_b64"
      ],
      "properties": {
        "type": {
          "const": "context.upload"
        },
        "v": {
          "const": 1
        },
        "id": {
          "$ref": "#/$defs/MessageId"
        },
        "name": {
          "$ref": "#/$defs/FileName"
        },
        "content_b64": {
          "$ref": "#/$defs/Base64Data",
          "type": "string",
          "maxLength": 6990508
        }
      }
    },
    "ContextRemove": {
      "type": "object",
      "required": [
        "type",
        "v",
        "id",
        "doc_id"
      ],
      "properties": {
        "type": {
          "const": "context.remove"
        },
        "v": {
          "const": 1
        },
        "id": {
          "$ref": "#/$defs/MessageId"
        },
        "doc_id": {
          "$ref": "#/$defs/DocId"
        }
      }
    },
    "LearningForget": {
      "type": "object",
      "required": [
        "type",
        "v",
        "id",
        "decision_id"
      ],
      "properties": {
        "type": {
          "const": "learning.forget"
        },
        "v": {
          "const": 1
        },
        "id": {
          "$ref": "#/$defs/MessageId"
        },
        "decision_id": {
          "$ref": "#/$defs/DecisionId"
        }
      }
    },
    "LearningForgetAll": {
      "type": "object",
      "required": [
        "type",
        "v",
        "id"
      ],
      "properties": {
        "type": {
          "const": "learning.forget_all"
        },
        "v": {
          "const": 1
        },
        "id": {
          "$ref": "#/$defs/MessageId"
        }
      }
    },
    "Unpair": {
      "type": "object",
      "required": [
        "type",
        "v",
        "id"
      ],
      "properties": {
        "type": {
          "const": "unpair"
        },
        "v": {
          "const": 1
        },
        "id": {
          "$ref": "#/$defs/MessageId"
        }
      }
    },
    "Chunk": {
      "type": "object",
      "required": [
        "type",
        "v",
        "chunk_id",
        "index",
        "count",
        "data"
      ],
      "properties": {
        "type": {
          "const": "chunk"
        },
        "v": {
          "const": 1
        },
        "chunk_id": {
          "$ref": "#/$defs/MessageId"
        },
        "index": {
          "type": "integer",
          "minimum": 0,
          "maximum": 255
        },
        "count": {
          "type": "integer",
          "minimum": 1,
          "maximum": 256
        },
        "data": {
          "$ref": "#/$defs/Base64Data",
          "type": "string",
          "maxLength": 43692
        }
      }
    }
  }
}

export const envelopeSchema = {
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "Envelope",
  "type": "object",
  "oneOf": [
    {
      "$ref": "#/$defs/EnvelopeHello"
    },
    {
      "$ref": "#/$defs/EnvelopeData"
    }
  ],
  "discriminator": {
    "propertyName": "kind"
  },
  "$defs": {
    "Channel": {
      "type": "string",
      "pattern": "^[A-Za-z0-9_-]{42}[AEIMQUYcgkosw048]$",
      "maxLength": 43
    },
    "Sid": {
      "type": "string",
      "pattern": "^[A-Za-z0-9_-]{21}[AQgw]$",
      "maxLength": 22
    },
    "Seq": {
      "type": "integer",
      "minimum": 1,
      "maximum": 4294967295
    },
    "Base64Url": {
      "type": "string",
      "pattern": "^(?:[A-Za-z0-9_-]{4})*(?:[A-Za-z0-9_-][AQgw]|[A-Za-z0-9_-]{2}[AEIMQUYcgkosw048])?$",
      "not": {
        "pattern": "[^A-Za-z0-9_-]"
      }
    },
    "EnvelopeHello": {
      "type": "object",
      "required": [
        "v",
        "channel",
        "sid",
        "seq",
        "kind",
        "ciphertext"
      ],
      "properties": {
        "v": {
          "const": 1
        },
        "channel": {
          "$ref": "#/$defs/Channel"
        },
        "sid": {
          "$ref": "#/$defs/Sid"
        },
        "seq": {
          "const": 0
        },
        "kind": {
          "const": "hello"
        },
        "ciphertext": {
          "$ref": "#/$defs/Base64Url",
          "type": "string",
          "minLength": 4,
          "maxLength": 5462
        }
      }
    },
    "EnvelopeData": {
      "type": "object",
      "required": [
        "v",
        "channel",
        "sid",
        "seq",
        "kind",
        "ciphertext"
      ],
      "properties": {
        "v": {
          "const": 1
        },
        "channel": {
          "$ref": "#/$defs/Channel"
        },
        "sid": {
          "$ref": "#/$defs/Sid"
        },
        "seq": {
          "$ref": "#/$defs/Seq"
        },
        "kind": {
          "const": "data"
        },
        "ciphertext": {
          "$ref": "#/$defs/Base64Url",
          "type": "string",
          "minLength": 22,
          "maxLength": 61440
        }
      }
    }
  }
}
