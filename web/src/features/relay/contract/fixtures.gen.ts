import type { Envelope } from './envelope.gen'
import type { Payload } from './payload.gen'

export const envelopeFixtures = {
  "data": {
    "v": 1,
    "channel": "SORPr2-cMZOrImFCL8a5yjlUDl5ARNCE5NuRaDwI_A0",
    "sid": "AAECAwQFBgcICQoLDA0ODw",
    "seq": 1,
    "kind": "data",
    "ciphertext": "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0-Pw"
  } satisfies Envelope,
  "hello": {
    "v": 1,
    "channel": "SORPr2-cMZOrImFCL8a5yjlUDl5ARNCE5NuRaDwI_A0",
    "sid": "AAECAwQFBgcICQoLDA0ODw",
    "seq": 0,
    "kind": "hello",
    "ciphertext": "eyJ0IjoiYWJvcnQifQ"
  } satisfies Envelope,
}

export const payloadFixtures = {
  "ad.mark": {
    "type": "ad.mark",
    "v": 1,
    "id": "00000000-0000-4000-8000-000000000003",
    "ad_id": "finn-401234567",
    "mark": "not_for_me"
  } satisfies Payload,
  "ad.show_anyway": {
    "type": "ad.show_anyway",
    "v": 1,
    "id": "00000000-0000-4000-8000-000000000005",
    "ad_id": "teamtailor-5521",
    "on": true
  } satisfies Payload,
  "ad.unmark": {
    "type": "ad.unmark",
    "v": 1,
    "id": "00000000-0000-4000-8000-000000000004",
    "ad_id": "nav-7f3c2a"
  } satisfies Payload,
  "ads.snapshot": {
    "type": "ads.snapshot",
    "v": 1,
    "generated_at": "2026-10-12T03:05:00Z",
    "as_of": "2026-10-12",
    "active": [
      {
        "id": "finn-401234567",
        "company": "Employer One AS",
        "title": "Data Engineer",
        "place": "Trondheim",
        "role_type": "data_engineering",
        "source": "finn",
        "published_at": "2026-10-06T07:30:00Z",
        "found_at": "2026-10-06T09:00:00Z",
        "url": "https://jobs.example.test/ad/401234567",
        "deadline": "2026-10-20",
        "fit": "strong",
        "is_new": false,
        "today": true,
        "summary": "Build pipelines and data models for a new group-wide data platform, reporting to the CTO.",
        "reason": "Python and SQL pipelines match the data platform work.",
        "gaps": [
          "No Airflow in the profile"
        ],
        "checks": [
          {
            "text": "Python",
            "state": "met"
          },
          {
            "text": "Kafka",
            "state": "partly"
          },
          {
            "text": "Airflow",
            "state": "missing"
          }
        ],
        "mark": null,
        "marked_at": null,
        "shown_anyway": false
      },
      {
        "id": "nav-7f3c2a",
        "company": "Employer Two AS",
        "title": "Fullstack Developer",
        "place": "",
        "role_type": "fullstack",
        "source": "nav",
        "published_at": null,
        "found_at": "2026-10-12T03:01:00Z",
        "url": "https://jobs.example.test/ad/7f3c2a",
        "deadline": null,
        "fit": "partial",
        "is_new": true,
        "today": true,
        "summary": "",
        "reason": "React work fits, the backend is Go.",
        "gaps": [],
        "checks": [],
        "mark": "applied",
        "marked_at": "2026-10-12T07:45:00Z",
        "shown_anyway": true
      }
    ],
    "active_total": 120,
    "hidden": [
      {
        "id": "teamtailor-5521",
        "company": "Org Two AS",
        "title": "Senior Backend Engineer",
        "place": "Oslo",
        "role_type": "backend",
        "source": "teamtailor",
        "url": "https://jobs.example.test/ad/5521",
        "found_at": "2026-10-11T03:02:00Z",
        "today": false,
        "kind": "years",
        "reason": "Requires 5 years"
      },
      {
        "id": "nav-9911",
        "company": "Org Three AS",
        "title": "IT-driftstekniker",
        "place": "Bergen",
        "role_type": null,
        "source": "nav",
        "url": "https://jobs.example.test/ad/9911",
        "found_at": "2026-10-12T03:01:00Z",
        "today": true,
        "kind": "other",
        "reason": "Other area: IT operations"
      }
    ],
    "hidden_total": 2,
    "hidden_counts": {
      "senior": 0,
      "years": 1,
      "masters": 0,
      "role": 0,
      "place": 0,
      "other": 1
    },
    "closed": [
      {
        "id": "finn-398877665",
        "closed_on": "2026-10-08",
        "company": "Employer Three AS",
        "title": "Software Engineer",
        "source": "finn",
        "reason": "removed",
        "mark": "none"
      }
    ],
    "closed_total": 9
  } satisfies Payload,
  "ads.snapshot.on-request": {
    "type": "ads.snapshot",
    "v": 1,
    "generated_at": "2026-10-12T03:05:00Z",
    "as_of": "2026-10-12",
    "active": [
      {
        "id": "finn-401234567",
        "company": "Employer One AS",
        "title": "Data Engineer",
        "place": "Trondheim",
        "role_type": "data_engineering",
        "source": "finn",
        "published_at": "2026-10-06T07:30:00Z",
        "found_at": "2026-10-06T09:00:00Z",
        "url": "https://jobs.example.test/ad/401234567",
        "deadline": "2026-10-20",
        "fit": "on_request",
        "is_new": false,
        "today": true,
        "summary": "Build pipelines and data models for a new group-wide data platform, reporting to the CTO.",
        "reason": "Python and SQL pipelines match the data platform work.",
        "gaps": [
          "No Airflow in the profile"
        ],
        "checks": [
          {
            "text": "Python",
            "state": "met"
          },
          {
            "text": "Kafka",
            "state": "partly"
          },
          {
            "text": "Airflow",
            "state": "missing"
          }
        ],
        "mark": null,
        "marked_at": null,
        "shown_anyway": true
      }
    ],
    "active_total": 1,
    "hidden": [],
    "hidden_total": 0,
    "hidden_counts": {
      "senior": 0,
      "years": 0,
      "masters": 0,
      "role": 0,
      "place": 0,
      "other": 0
    },
    "closed": [],
    "closed_total": 0
  } satisfies Payload,
  "ads.snapshot.truncated": {
    "type": "ads.snapshot",
    "v": 1,
    "generated_at": "2026-10-12T03:05:00Z",
    "as_of": "2026-10-12",
    "active": [
      {
        "id": "finn-401234567",
        "company": "Employer One AS",
        "title": "Data Engineer",
        "place": "Trondheim",
        "role_type": "data_engineering",
        "source": "finn",
        "published_at": "2026-10-06T07:30:00Z",
        "found_at": "2026-10-06T09:00:00Z",
        "url": "https://jobs.example.test/ad/401234567",
        "deadline": "2026-10-20",
        "fit": "strong",
        "is_new": false,
        "today": true,
        "summary": "Build pipelines and data models for a new group-wide data platform, reporting to the CTO.",
        "reason": "Python and SQL pipelines match the data platform work.",
        "gaps": [
          "No Airflow in the profile"
        ],
        "checks": [
          {
            "text": "Python",
            "state": "met"
          },
          {
            "text": "Kafka",
            "state": "partly"
          },
          {
            "text": "Airflow",
            "state": "missing"
          }
        ],
        "mark": null,
        "marked_at": null,
        "shown_anyway": false
      },
      {
        "id": "nav-7f3c2a",
        "company": "Employer Two AS",
        "title": "Fullstack Developer",
        "place": "",
        "role_type": "fullstack",
        "source": "nav",
        "published_at": null,
        "found_at": "2026-10-12T03:01:00Z",
        "url": "https://jobs.example.test/ad/7f3c2a",
        "deadline": null,
        "fit": "partial",
        "is_new": true,
        "today": true,
        "summary": "",
        "reason": "React work fits, the backend is Go.",
        "gaps": [],
        "checks": [],
        "mark": "applied",
        "marked_at": "2026-10-12T07:45:00Z",
        "shown_anyway": true
      }
    ],
    "active_total": 120,
    "hidden": [
      {
        "id": "teamtailor-5521",
        "company": "Org Two AS",
        "title": "Senior Backend Engineer",
        "place": "Oslo",
        "role_type": "backend",
        "source": "teamtailor",
        "url": "https://jobs.example.test/ad/5521",
        "found_at": "2026-10-11T03:02:00Z",
        "today": false,
        "kind": "years",
        "reason": "Requires 5 years"
      },
      {
        "id": "nav-9911",
        "company": "Org Three AS",
        "title": "IT-driftstekniker",
        "place": "Bergen",
        "role_type": null,
        "source": "nav",
        "url": "https://jobs.example.test/ad/9911",
        "found_at": "2026-10-12T03:01:00Z",
        "today": true,
        "kind": "other",
        "reason": "Other area: IT operations"
      }
    ],
    "hidden_total": 41,
    "hidden_counts": {
      "senior": 12,
      "years": 9,
      "masters": 3,
      "role": 8,
      "place": 4,
      "other": 5
    },
    "closed": [
      {
        "id": "finn-398877665",
        "closed_on": "2026-10-08",
        "company": "Employer Three AS",
        "title": "Software Engineer",
        "source": "finn",
        "reason": "removed",
        "mark": "none"
      }
    ],
    "closed_total": 9
  } satisfies Payload,
  "application.create": {
    "type": "application.create",
    "v": 1,
    "id": "00000000-0000-4000-8000-000000000006",
    "company": "Org One AS",
    "title": "Backend Developer",
    "note": "Sent through the company site"
  } satisfies Payload,
  "application.create.long-text": {
    "type": "application.create",
    "v": 1,
    "id": "00000000-0000-4000-8000-000000000006",
    "company": "Employer One AS Employer One AS Employer One AS Employer One AS Employer One AS Employer One AS Employer One AS Employer One AS Employer One AS Employer One AS Employer One AS Employer One AS Employe ",
    "title": "Fullstack Developer Fullstack Developer Fullstack Developer Fullstack Developer Fullstack Developer Fullstack Developer Fullstack Developer Fullstack Developer Fullstack Developer Fullstack Developer ",
    "note": "Sent through the company site"
  } satisfies Payload,
  "application.remove": {
    "type": "application.remove",
    "v": 1,
    "id": "00000000-0000-4000-8000-000000000008",
    "application_id": 2,
    "removed": true
  } satisfies Payload,
  "application.update": {
    "type": "application.update",
    "v": 1,
    "id": "00000000-0000-4000-8000-000000000007",
    "application_id": 2,
    "status": "interview",
    "note": "Second round on Friday"
  } satisfies Payload,
  "applications.snapshot.general-software": {
    "type": "applications.snapshot",
    "v": 1,
    "generated_at": "2026-10-12T07:45:01Z",
    "applications": [
      {
        "id": 3,
        "ad_id": "nav-7f3c2a",
        "company": "Employer Two AS",
        "title": "Fullstack Developer",
        "place": "",
        "role_type": "general_software",
        "source": "nav",
        "url": "https://jobs.example.test/ad/7f3c2a",
        "deadline": "2026-10-25",
        "fit": "partial",
        "status": "waiting",
        "note": "",
        "applied_at": "2026-10-12T07:45:00Z",
        "found_at": "2026-10-12T03:01:00Z",
        "cv_name": "cv.pdf",
        "checks": [
          {
            "text": "React",
            "state": "met"
          }
        ],
        "history": [
          {
            "at": "2026-10-12T03:01:00Z",
            "text": "Found by jobranger"
          },
          {
            "at": "2026-10-12T07:45:00Z",
            "text": "Applied"
          }
        ],
        "removed": false
      },
      {
        "id": 2,
        "ad_id": null,
        "company": "Org One AS",
        "title": "",
        "place": "",
        "role_type": null,
        "source": null,
        "url": null,
        "deadline": null,
        "fit": null,
        "status": "interview",
        "note": "Second round on Friday",
        "applied_at": "2026-10-01T10:00:00Z",
        "found_at": null,
        "cv_name": null,
        "checks": [],
        "history": [
          {
            "at": "2026-10-01T10:00:00Z",
            "text": "Added by hand"
          }
        ],
        "removed": true
      }
    ],
    "applications_total": 212
  } satisfies Payload,
  "applications.snapshot": {
    "type": "applications.snapshot",
    "v": 1,
    "generated_at": "2026-10-12T07:45:01Z",
    "applications": [
      {
        "id": 3,
        "ad_id": "nav-7f3c2a",
        "company": "Employer Two AS",
        "title": "Fullstack Developer",
        "place": "",
        "role_type": "fullstack",
        "source": "nav",
        "url": "https://jobs.example.test/ad/7f3c2a",
        "deadline": "2026-10-25",
        "fit": "partial",
        "status": "waiting",
        "note": "",
        "applied_at": "2026-10-12T07:45:00Z",
        "found_at": "2026-10-12T03:01:00Z",
        "cv_name": "cv.pdf",
        "checks": [
          {
            "text": "React",
            "state": "met"
          }
        ],
        "history": [
          {
            "at": "2026-10-12T03:01:00Z",
            "text": "Found by jobranger"
          },
          {
            "at": "2026-10-12T07:45:00Z",
            "text": "Applied"
          }
        ],
        "removed": false
      },
      {
        "id": 2,
        "ad_id": null,
        "company": "Org One AS",
        "title": "",
        "place": "",
        "role_type": null,
        "source": null,
        "url": null,
        "deadline": null,
        "fit": null,
        "status": "interview",
        "note": "Second round on Friday",
        "applied_at": "2026-10-01T10:00:00Z",
        "found_at": null,
        "cv_name": null,
        "checks": [],
        "history": [
          {
            "at": "2026-10-01T10:00:00Z",
            "text": "Added by hand"
          }
        ],
        "removed": true
      }
    ],
    "applications_total": 212
  } satisfies Payload,
  "applications.snapshot.long-text": {
    "type": "applications.snapshot",
    "v": 1,
    "generated_at": "2026-10-12T07:45:01Z",
    "applications": [
      {
        "id": 3,
        "ad_id": "nav-7f3c2a",
        "company": "Employer One AS Employer One AS Employer One AS Employer One AS Employer One AS Employer One AS Employer One AS Employer One AS Employer One AS Employer One AS Employer One AS Employer One AS Employe ",
        "title": "Fullstack Developer Fullstack Developer Fullstack Developer Fullstack Developer Fullstack Developer Fullstack Developer Fullstack Developer Fullstack Developer Fullstack Developer Fullstack Developer ",
        "place": "",
        "role_type": "fullstack",
        "source": "nav",
        "url": "https://jobs.example.test/ad/7f3c2a",
        "deadline": "2026-10-25",
        "fit": "partial",
        "status": "waiting",
        "note": "",
        "applied_at": "2026-10-12T07:45:00Z",
        "found_at": "2026-10-12T03:01:00Z",
        "cv_name": "cv.pdf",
        "checks": [
          {
            "text": "React",
            "state": "met"
          }
        ],
        "history": [
          {
            "at": "2026-10-12T03:01:00Z",
            "text": "Found by jobranger"
          },
          {
            "at": "2026-10-12T07:45:00Z",
            "text": "Applied"
          }
        ],
        "removed": false
      },
      {
        "id": 2,
        "ad_id": null,
        "company": "Org One AS",
        "title": "",
        "place": "",
        "role_type": null,
        "source": null,
        "url": null,
        "deadline": null,
        "fit": null,
        "status": "interview",
        "note": "Second round on Friday",
        "applied_at": "2026-10-01T10:00:00Z",
        "found_at": null,
        "cv_name": null,
        "checks": [],
        "history": [
          {
            "at": "2026-10-01T10:00:00Z",
            "text": "Added by hand"
          }
        ],
        "removed": true
      }
    ],
    "applications_total": 212
  } satisfies Payload,
  "chunk": {
    "type": "chunk",
    "v": 1,
    "chunk_id": "00000000-0000-4000-8000-000000000022",
    "index": 0,
    "count": 2,
    "data": "eyJ0eXBlIjoiYWRzLnNuYXBzaG90IiwidiI6MSw="
  } satisfies Payload,
  "config.snapshot.empty": {
    "type": "config.snapshot",
    "v": 1,
    "roles": [
      {
        "id": "backend",
        "label": "Backend"
      },
      {
        "id": "frontend",
        "label": "Frontend"
      },
      {
        "id": "data_engineering",
        "label": "Data engineering"
      }
    ],
    "targets": {
      "subfields": [
        "backend",
        "data_engineering"
      ],
      "levels": [
        "junior",
        "mid"
      ],
      "years": [
        "0-1",
        "1-3"
      ],
      "hide_masters": true,
      "places": [
        "Bergen",
        "Oslo"
      ],
      "relocate": false
    },
    "sources": {
      "nav": true,
      "employers": true,
      "finn": false,
      "linkedin": false
    },
    "schedule": {
      "times": [
        "03:00",
        "15:00"
      ],
      "min_interval_hours": 2
    },
    "notify": {
      "website": true,
      "email": {
        "on": false,
        "address": null
      }
    },
    "model": {
      "provider": null,
      "model": null,
      "key_hint": null,
      "price": {
        "input_per_mtok": 0,
        "output_per_mtok": 0
      },
      "monthly_limit_usd": null
    }
  } satisfies Payload,
  "config.snapshot": {
    "type": "config.snapshot",
    "v": 1,
    "roles": [
      {
        "id": "backend",
        "label": "Backend"
      },
      {
        "id": "frontend",
        "label": "Frontend"
      },
      {
        "id": "data_engineering",
        "label": "Data engineering"
      }
    ],
    "targets": {
      "subfields": [
        "backend",
        "data_engineering"
      ],
      "levels": [
        "junior",
        "mid"
      ],
      "years": [
        "0-1",
        "1-3"
      ],
      "hide_masters": true,
      "places": [
        "Bergen",
        "Oslo"
      ],
      "relocate": false
    },
    "sources": {
      "nav": true,
      "employers": true,
      "finn": false,
      "linkedin": false
    },
    "schedule": {
      "times": [
        "03:00",
        "15:00"
      ],
      "min_interval_hours": 2
    },
    "notify": {
      "website": true,
      "email": {
        "on": true,
        "address": "user@example.com"
      }
    },
    "model": {
      "provider": "anthropic",
      "model": "model-one",
      "key_hint": "a1b2",
      "price": {
        "input_per_mtok": 0.8,
        "output_per_mtok": 4.5
      },
      "monthly_limit_usd": 20
    }
  } satisfies Payload,
  "config.update.full": {
    "type": "config.update",
    "v": 1,
    "id": "00000000-0000-4000-8000-000000000009",
    "patch": {
      "targets": {
        "subfields": [
          "backend",
          "fullstack"
        ],
        "levels": [
          "junior"
        ],
        "years": [
          "0-1",
          "1-3"
        ],
        "hide_masters": false,
        "places": [
          "Bergen"
        ],
        "relocate": true
      },
      "sources": {
        "nav": true,
        "employers": false,
        "finn": true,
        "linkedin": false
      },
      "schedule": {
        "times": [
          "06:00"
        ]
      },
      "notify": {
        "website": false,
        "email": {
          "on": true,
          "address": "user@example.com"
        }
      },
      "model": {
        "price": {
          "input_per_mtok": 0.8,
          "output_per_mtok": 4.5
        },
        "monthly_limit_usd": null
      }
    }
  } satisfies Payload,
  "config.update": {
    "type": "config.update",
    "v": 1,
    "id": "00000000-0000-4000-8000-000000000009",
    "patch": {
      "targets": {
        "levels": [
          "junior",
          "mid"
        ],
        "places": [
          "Bergen",
          "Oslo"
        ]
      },
      "schedule": {
        "times": [
          "03:00",
          "15:00"
        ]
      },
      "notify": {
        "email": {
          "address": null
        }
      },
      "model": {
        "price": {
          "input_per_mtok": 0.8
        },
        "monthly_limit_usd": 15
      }
    }
  } satisfies Payload,
  "context.remove": {
    "type": "context.remove",
    "v": 1,
    "id": "00000000-0000-4000-8000-000000000018",
    "doc_id": "doc_portfolio"
  } satisfies Payload,
  "context.snapshot": {
    "type": "context.snapshot",
    "v": 1,
    "about": "Moving to Bergen in January.",
    "about_limit": 4000,
    "docs": [
      {
        "id": "doc_portfolio",
        "name": "portfolio.md",
        "chars": 2400,
        "added_at": "2026-10-10"
      }
    ],
    "docs_limit": 10
  } satisfies Payload,
  "context.update": {
    "type": "context.update",
    "v": 1,
    "id": "00000000-0000-4000-8000-000000000016",
    "about": "Moving to Bergen in January."
  } satisfies Payload,
  "context.upload": {
    "type": "context.upload",
    "v": 1,
    "id": "00000000-0000-4000-8000-000000000017",
    "name": "portfolio.md",
    "content_b64": "IyBQb3J0Zm9saW8K"
  } satisfies Payload,
  "cv.upload": {
    "type": "cv.upload",
    "v": 1,
    "id": "00000000-0000-4000-8000-000000000012",
    "name": "cv.pdf",
    "size_bytes": 9,
    "content_b64": "JVBERi0xLjcK"
  } satisfies Payload,
  "error": {
    "type": "error",
    "v": 1,
    "ref": "00000000-0000-4000-8000-000000000009",
    "code": "invalid_payload",
    "message": "Run times must be at least 2 hours apart",
    "field": "patch.schedule.times"
  } satisfies Payload,
  "error.unprompted": {
    "type": "error",
    "v": 1,
    "ref": null,
    "code": "chunk_incomplete",
    "message": "Chunk set incomplete: 1 of 2 received",
    "field": null
  } satisfies Payload,
  "guidance.reset": {
    "type": "guidance.reset",
    "v": 1,
    "id": "00000000-0000-4000-8000-000000000015"
  } satisfies Payload,
  "guidance.snapshot.default": {
    "type": "guidance.snapshot",
    "v": 1,
    "text": "",
    "is_default": true,
    "limit": 12000
  } satisfies Payload,
  "guidance.snapshot": {
    "type": "guidance.snapshot",
    "v": 1,
    "text": "Prefer backend roles with Python.\nTreat consulting roles as a partial fit.",
    "is_default": false,
    "limit": 12000
  } satisfies Payload,
  "guidance.update": {
    "type": "guidance.update",
    "v": 1,
    "id": "00000000-0000-4000-8000-000000000014",
    "text": "Prefer backend roles with Python."
  } satisfies Payload,
  "key.replace": {
    "type": "key.replace",
    "v": 1,
    "id": "00000000-0000-4000-8000-000000000011",
    "provider": "anthropic",
    "key": "test-key-0000"
  } satisfies Payload,
  "learning.forget": {
    "type": "learning.forget",
    "v": 1,
    "id": "00000000-0000-4000-8000-000000000019",
    "decision_id": 12
  } satisfies Payload,
  "learning.forget_all": {
    "type": "learning.forget_all",
    "v": 1,
    "id": "00000000-0000-4000-8000-000000000020"
  } satisfies Payload,
  "learning.snapshot": {
    "type": "learning.snapshot",
    "v": 1,
    "state": "collecting",
    "unavailable_reason": null,
    "decision_count": 7,
    "cold_start_threshold": 20,
    "examples": [
      {
        "decision_id": 12,
        "ad_id": "finn-398877665",
        "company": "Employer Three AS",
        "title": "Software Engineer",
        "decision": "not_for_me",
        "at": "2026-10-08T19:10:00Z"
      }
    ],
    "examples_total": 7
  } satisfies Payload,
  "learning.snapshot.unavailable": {
    "type": "learning.snapshot",
    "v": 1,
    "state": "unavailable",
    "unavailable_reason": "No onnxruntime build for this machine",
    "decision_count": 0,
    "cold_start_threshold": 20,
    "examples": [],
    "examples_total": 0
  } satisfies Payload,
  "model.select": {
    "type": "model.select",
    "v": 1,
    "id": "00000000-0000-4000-8000-000000000010",
    "provider": "openrouter",
    "model": "model-one"
  } satisfies Payload,
  "models.snapshot": {
    "type": "models.snapshot",
    "v": 1,
    "providers": [
      {
        "provider": "anthropic",
        "error": null,
        "models": [
          {
            "id": "model-one",
            "name": "Model One",
            "input_per_mtok": 0.8,
            "output_per_mtok": 4.5,
            "usd_per_day": 0.21
          }
        ]
      },
      {
        "provider": "ollama",
        "error": null,
        "models": [
          {
            "id": "local-model",
            "name": "local-model",
            "input_per_mtok": null,
            "output_per_mtok": null,
            "usd_per_day": null
          }
        ]
      },
      {
        "provider": "openai",
        "error": "The provider refused the key",
        "models": []
      }
    ]
  } satisfies Payload,
  "profile.snapshot.empty": {
    "type": "profile.snapshot",
    "v": 1,
    "cv_name": null,
    "cv_read_at": null,
    "summary": "",
    "education": "",
    "languages": [],
    "stack": [],
    "projects": []
  } satisfies Payload,
  "profile.snapshot": {
    "type": "profile.snapshot",
    "v": 1,
    "cv_name": "cv.pdf",
    "cv_read_at": "2026-10-10T18:20:00Z",
    "summary": "Two years of backend work in Python and C#.",
    "education": "BSc Computer Science",
    "languages": [
      "Norwegian",
      "English"
    ],
    "stack": [
      "Python",
      "TypeScript",
      "C#"
    ],
    "projects": [
      {
        "id": "p1",
        "name": "Job scraper"
      }
    ]
  } satisfies Payload,
  "profile.update.full": {
    "type": "profile.update",
    "v": 1,
    "id": "00000000-0000-4000-8000-000000000013",
    "summary": "Two years of backend work in Python and C#.",
    "stack": [
      "Python",
      "TypeScript"
    ],
    "projects": [
      {
        "id": "p1",
        "name": "Job scraper"
      },
      {
        "id": null,
        "name": "Relay"
      }
    ],
    "education": "BSc Computer Science",
    "languages": [
      "Norwegian",
      "English"
    ]
  } satisfies Payload,
  "profile.update": {
    "type": "profile.update",
    "v": 1,
    "id": "00000000-0000-4000-8000-000000000013",
    "summary": "Two years of backend work in Python and C#.",
    "stack": [
      "Python",
      "TypeScript"
    ],
    "projects": [
      {
        "id": "p1",
        "name": "Job scraper"
      },
      {
        "id": null,
        "name": "Relay"
      }
    ]
  } satisfies Payload,
  "run.cancel": {
    "type": "run.cancel",
    "v": 1,
    "id": "00000000-0000-4000-8000-000000000002",
    "run_id": 42
  } satisfies Payload,
  "run.cancelled": {
    "type": "run.cancelled",
    "v": 1,
    "run_id": 42,
    "at": "2026-10-12T13:02:00Z"
  } satisfies Payload,
  "run.finished": {
    "type": "run.finished",
    "v": 1,
    "run_id": 42,
    "finished_at": "2026-10-12T13:06:00Z",
    "ads_read": 812,
    "new_ads": 37,
    "matches": 6,
    "hidden": 31,
    "cost_usd": 0.41,
    "failed_sources": [
      "linkedin"
    ],
    "budget_paused": false
  } satisfies Payload,
  "run.progress.batch": {
    "type": "run.progress",
    "v": 1,
    "run_id": 42,
    "mode": "manual",
    "started_at": "2026-10-12T13:00:00Z",
    "percent": 70,
    "sources": [
      {
        "source": "nav",
        "state": "done",
        "found_new": 12
      },
      {
        "source": "finn",
        "state": "failed",
        "found_new": null
      }
    ],
    "model": {
      "state": "active",
      "checked": 10,
      "total": 37,
      "results_by": "2026-10-12T13:20:00Z"
    }
  } satisfies Payload,
  "run.progress": {
    "type": "run.progress",
    "v": 1,
    "run_id": 42,
    "mode": "manual",
    "started_at": "2026-10-12T13:00:00Z",
    "percent": 40,
    "sources": [
      {
        "source": "nav",
        "state": "done",
        "found_new": 12
      },
      {
        "source": "teamtailor",
        "state": "active",
        "found_new": null
      },
      {
        "source": "finn",
        "state": "pending",
        "found_new": null
      }
    ],
    "model": {
      "state": "pending",
      "checked": 0,
      "total": 0,
      "results_by": null
    }
  } satisfies Payload,
  "run.start": {
    "type": "run.start",
    "v": 1,
    "id": "00000000-0000-4000-8000-000000000001"
  } satisfies Payload,
  "saved": {
    "type": "saved",
    "v": 1,
    "ref": "00000000-0000-4000-8000-000000000014",
    "applies_from": "next_run",
    "recheck": {
      "ads": 37,
      "cost_usd": 0.12
    }
  } satisfies Payload,
  "saved.unpriced": {
    "type": "saved",
    "v": 1,
    "ref": "00000000-0000-4000-8000-000000000014",
    "applies_from": "next_run",
    "recheck": {
      "ads": 37,
      "cost_usd": null
    }
  } satisfies Payload,
  "status.idle": {
    "type": "status",
    "v": 1,
    "cli_version": "0.1.0",
    "contract_version": 1,
    "machine": "laptop",
    "paired_at": "2026-10-09T08:12:00Z",
    "last_run": null,
    "next_run_at": null,
    "running": null,
    "budget_paused": false
  } satisfies Payload,
  "status": {
    "type": "status",
    "v": 1,
    "cli_version": "0.1.0",
    "contract_version": 1,
    "machine": "laptop",
    "paired_at": "2026-10-09T08:12:00Z",
    "last_run": {
      "run_id": 41,
      "finished_at": "2026-10-12T03:04:00Z",
      "ads_read": 812,
      "matches": 6,
      "cost_usd": 0.41
    },
    "next_run_at": "2026-10-12T13:00:00Z",
    "running": {
      "run_id": 42,
      "mode": "scheduled",
      "started_at": "2026-10-12T13:00:00Z",
      "results_by": "2026-10-12T13:20:00Z"
    },
    "budget_paused": false
  } satisfies Payload,
  "status.manual-run": {
    "type": "status",
    "v": 1,
    "cli_version": "0.1.0",
    "contract_version": 1,
    "machine": "laptop",
    "paired_at": "2026-10-09T08:12:00Z",
    "last_run": {
      "run_id": 41,
      "finished_at": "2026-10-12T03:04:00Z",
      "ads_read": 812,
      "matches": 6,
      "cost_usd": 0.41
    },
    "next_run_at": "2026-10-12T13:00:00Z",
    "running": {
      "run_id": 42,
      "mode": "manual",
      "started_at": "2026-10-12T13:00:00Z",
      "results_by": null
    },
    "budget_paused": false
  } satisfies Payload,
  "unpair": {
    "type": "unpair",
    "v": 1,
    "id": "00000000-0000-4000-8000-000000000021"
  } satisfies Payload,
  "unpaired": {
    "type": "unpaired",
    "v": 1,
    "reason": "web"
  } satisfies Payload,
  "usage.snapshot": {
    "type": "usage.snapshot",
    "v": 1,
    "month": "2026-10",
    "month_tokens": 1840000,
    "today_tokens": 210000,
    "month_usd": 2.35,
    "today_usd": 0.41,
    "resets_on": "2026-11-01",
    "days": [
      {
        "day": "2026-10-11",
        "calls": 37,
        "input_tokens": 180000,
        "output_tokens": 12000,
        "cost_usd": 0.2,
        "ads_read": 790,
        "ads_checked": 37
      },
      {
        "day": "2026-10-12",
        "calls": 41,
        "input_tokens": 196000,
        "output_tokens": 14000,
        "cost_usd": 0.41,
        "ads_read": 812,
        "ads_checked": 41
      }
    ]
  } satisfies Payload,
}
