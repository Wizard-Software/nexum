window.BENCHMARK_DATA = {
  "lastUpdate": 1773534070853,
  "repoUrl": "https://github.com/asawicki/nexum",
  "entries": {
    "Benchmark": [
      {
        "commit": {
          "author": {
            "email": "artur.sawicki@gmail.com",
            "name": "asawicki",
            "username": "asawicki"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "5eb82c813831d43c08602a2778dc116ae0d03165",
          "message": "Merge pull request #2 from asawicki/Batching\n\nAdd Nexum.Batching project and update benchmarks references",
          "timestamp": "2026-02-14T22:17:51+01:00",
          "tree_id": "40bec29ebded269e7e6ceb1b623f3752650add51",
          "url": "https://github.com/asawicki/nexum/commit/5eb82c813831d43c08602a2778dc116ae0d03165"
        },
        "date": 1771104685301,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_SimpleCommand",
            "value": 72.82115550835927,
            "unit": "ns",
            "range": "± 0.03359939456931545"
          },
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_3Behaviors",
            "value": 192.35041499137878,
            "unit": "ns",
            "range": "± 4.815378159474651"
          },
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_5NotificationHandlers",
            "value": 133.76622319221497,
            "unit": "ns",
            "range": "± 0.2740335938025817"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "artur.sawicki@gmail.com",
            "name": "asawicki",
            "username": "asawicki"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "d7790c8ef1fc3f30e7acba544e49cc660851ddac",
          "message": "Merge pull request #3 from asawicki/MinimalApis\n\nAdd NuGet package metadata, versioning, and SourceLink support in Dir…",
          "timestamp": "2026-02-15T01:51:11+01:00",
          "tree_id": "9736eee49555bddaa10b28b4c8d368f1b2aa40f1",
          "url": "https://github.com/asawicki/nexum/commit/d7790c8ef1fc3f30e7acba544e49cc660851ddac"
        },
        "date": 1771116755841,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_SimpleCommand",
            "value": 77.99936705827713,
            "unit": "ns",
            "range": "± 2.4431979550406604"
          },
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_3Behaviors",
            "value": 196.89222939809164,
            "unit": "ns",
            "range": "± 1.1423601108130668"
          },
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_5NotificationHandlers",
            "value": 166.64211948712668,
            "unit": "ns",
            "range": "± 0.3739126286195366"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "artur.sawicki@gmail.com",
            "name": "asawicki",
            "username": "asawicki"
          },
          "committer": {
            "email": "artur.sawicki@gmail.com",
            "name": "asawicki",
            "username": "asawicki"
          },
          "distinct": true,
          "id": "001c72322471bdaa5e953bf112a480d9feaeca9f",
          "message": "Update CI configuration and benchmark settings: increase alert threshold to 130% and disable failure on alert; change benchmark job type from ShortRun to MediumRun for improved performance analysis.",
          "timestamp": "2026-02-15T02:02:43+01:00",
          "tree_id": "5ef68f1b85dec8f17ca8a05b2bb445c320ee00f7",
          "url": "https://github.com/asawicki/nexum/commit/001c72322471bdaa5e953bf112a480d9feaeca9f"
        },
        "date": 1771117542771,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_SimpleCommand",
            "value": 71.64032856965888,
            "unit": "ns",
            "range": "± 0.38624904459985027"
          },
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_3Behaviors",
            "value": 182.1855343703566,
            "unit": "ns",
            "range": "± 3.704763538043912"
          },
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_5NotificationHandlers",
            "value": 132.96385504459514,
            "unit": "ns",
            "range": "± 7.418588178518134"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "artur.sawicki@gmail.com",
            "name": "asawicki",
            "username": "asawicki"
          },
          "committer": {
            "email": "artur.sawicki@gmail.com",
            "name": "asawicki",
            "username": "asawicki"
          },
          "distinct": true,
          "id": "b33403b22adcab9ed62dae4490a50b11556d3108",
          "message": "Enhance CI configuration by adjusting alert thresholds and modifying benchmark job type for better performance analysis; streamline error handling in connection pool management for improved query simulation.",
          "timestamp": "2026-02-15T02:30:16+01:00",
          "tree_id": "c9af6448aaf75e998f83333f807f34be0b6ac123",
          "url": "https://github.com/asawicki/nexum/commit/b33403b22adcab9ed62dae4490a50b11556d3108"
        },
        "date": 1771119195291,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_SimpleCommand",
            "value": 77.13221165963581,
            "unit": "ns",
            "range": "± 1.7720720155486362"
          },
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_3Behaviors",
            "value": 181.31003986555953,
            "unit": "ns",
            "range": "± 2.148461318081788"
          },
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_5NotificationHandlers",
            "value": 139.26965787581034,
            "unit": "ns",
            "range": "± 2.954589462608372"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "artur.sawicki@gmail.com",
            "name": "asawicki",
            "username": "asawicki"
          },
          "committer": {
            "email": "artur.sawicki@gmail.com",
            "name": "asawicki",
            "username": "asawicki"
          },
          "distinct": true,
          "id": "fc48fd52a3f81ae1b783ac3d513c9fcd8c35bf29",
          "message": "Enhance GitHub Actions workflow by adding support for release events, allowing automated actions upon package publication.",
          "timestamp": "2026-02-15T02:36:45+01:00",
          "tree_id": "2d8e6b3528c2c01dcfc9456f284e52395f697ea5",
          "url": "https://github.com/asawicki/nexum/commit/fc48fd52a3f81ae1b783ac3d513c9fcd8c35bf29"
        },
        "date": 1771119586041,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_SimpleCommand",
            "value": 74.74597978591919,
            "unit": "ns",
            "range": "± 0.4013320813116319"
          },
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_3Behaviors",
            "value": 191.1962427775065,
            "unit": "ns",
            "range": "± 7.167841850110644"
          },
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_5NotificationHandlers",
            "value": 143.65595094910984,
            "unit": "ns",
            "range": "± 15.684479884057408"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "artur.sawicki@gmail.com",
            "name": "asawicki",
            "username": "asawicki"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "9063cd01e3d2775d6c3816303c300d59844f7bab",
          "message": "Merge pull request #4 from asawicki/fix_tier3\n\nImplement scoped service resolution in Command and Query Dispatchers;…",
          "timestamp": "2026-02-27T20:52:06+01:00",
          "tree_id": "beaad44df3750fbf11837951d0f95b18b2799b9b",
          "url": "https://github.com/asawicki/nexum/commit/9063cd01e3d2775d6c3816303c300d59844f7bab"
        },
        "date": 1772222093979,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_SimpleCommand",
            "value": 68.85157468795776,
            "unit": "ns",
            "range": "± 0.07885786064991725"
          },
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_3Behaviors",
            "value": 176.828755798011,
            "unit": "ns",
            "range": "± 2.0912675726323737"
          },
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_5NotificationHandlers",
            "value": 128.46023848961138,
            "unit": "ns",
            "range": "± 2.254198168332087"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "artur.sawicki@gmail.com",
            "name": "asawicki",
            "username": "asawicki"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "25428204b8d86e187209710526dc145918231efd",
          "message": "Merge pull request #5 from asawicki/add_doc_improve_otel\n\nEnhance Nexum OpenTelemetry integration by implementing IInterceptabl…",
          "timestamp": "2026-02-28T12:18:16+01:00",
          "tree_id": "a67494140665132842116db8f9dd982a7a3ae5a1",
          "url": "https://github.com/asawicki/nexum/commit/25428204b8d86e187209710526dc145918231efd"
        },
        "date": 1772277675922,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_SimpleCommand",
            "value": 79.32379741801157,
            "unit": "ns",
            "range": "± 5.396512423053209"
          },
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_3Behaviors",
            "value": 179.08069650880222,
            "unit": "ns",
            "range": "± 3.5425953270423336"
          },
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_5NotificationHandlers",
            "value": 134.4759254370417,
            "unit": "ns",
            "range": "± 7.358356907734152"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "artur.sawicki@gmail.com",
            "name": "asawicki",
            "username": "asawicki"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "04be0ec3033cf2b3ed4ce29b2d7f16b95a1ab5d8",
          "message": "Merge pull request #6 from asawicki/feature/migration-mediatr-completion\n\nFeature/migration mediatr completion",
          "timestamp": "2026-03-14T20:52:43+01:00",
          "tree_id": "b010536515851d26f3ad0edeb9823ea0f21d6ea2",
          "url": "https://github.com/asawicki/nexum/commit/04be0ec3033cf2b3ed4ce29b2d7f16b95a1ab5d8"
        },
        "date": 1773518166083,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_SimpleCommand",
            "value": 77.71895759304364,
            "unit": "ns",
            "range": "± 6.084816801844461"
          },
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_3Behaviors",
            "value": 208.03404686053594,
            "unit": "ns",
            "range": "± 7.978211537373282"
          },
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_5NotificationHandlers",
            "value": 174.43966684891626,
            "unit": "ns",
            "range": "± 2.7329046842989477"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "artur.sawicki@gmail.com",
            "name": "asawicki",
            "username": "asawicki"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "952eb47dae1bc949ec36d1f1a9462d48d8dee3c5",
          "message": "Merge pull request #7 from asawicki/feature/nexum-testing\n\nFeature/nexum testing",
          "timestamp": "2026-03-14T21:47:07+01:00",
          "tree_id": "e2f1624063aa304e1bdb68fdfb01bee2e9baa00b",
          "url": "https://github.com/asawicki/nexum/commit/952eb47dae1bc949ec36d1f1a9462d48d8dee3c5"
        },
        "date": 1773521414179,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_SimpleCommand",
            "value": 70.54195861980833,
            "unit": "ns",
            "range": "± 1.6988059900820758"
          },
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_3Behaviors",
            "value": 185.79620436032613,
            "unit": "ns",
            "range": "± 3.3606976967349182"
          },
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_5NotificationHandlers",
            "value": 167.0768824974696,
            "unit": "ns",
            "range": "± 20.792159704852935"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "artur.sawicki@gmail.com",
            "name": "asawicki",
            "username": "asawicki"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "90d2cc4514e31acc6da613edcc6e06cfedbd8f1b",
          "message": "Merge pull request #8 from asawicki/feature/r7.2-pipeline-delegate-caching\n\nFeature/r7.2 pipeline delegate caching",
          "timestamp": "2026-03-14T23:33:48+01:00",
          "tree_id": "c83a870187df34a686b904df880f9de316b5b7b8",
          "url": "https://github.com/asawicki/nexum/commit/90d2cc4514e31acc6da613edcc6e06cfedbd8f1b"
        },
        "date": 1773527761826,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_SimpleCommand",
            "value": 61.24736673633257,
            "unit": "ns",
            "range": "± 0.05454693980756342"
          },
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_3Behaviors",
            "value": 227.52928093274434,
            "unit": "ns",
            "range": "± 2.7096602190526964"
          },
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_SimpleQuery",
            "value": 49.54477240641912,
            "unit": "ns",
            "range": "± 0.049937050381202865"
          },
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_3NotificationHandlers_Sequential",
            "value": 138.32193190710885,
            "unit": "ns",
            "range": "± 0.25549533072829445"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "artur.sawicki@gmail.com",
            "name": "asawicki",
            "username": "asawicki"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "ff075a66d032d49991412bb9311b0c9441894985",
          "message": "Merge pull request #9 from asawicki/feature/r8.1-r8.3-nexum-streaming\n\nR8.1-R8.3: Add Nexum.Streaming package",
          "timestamp": "2026-03-15T01:18:56+01:00",
          "tree_id": "e2b3cf6703e90e8c9f4a88e95e93dd5232fb06ea",
          "url": "https://github.com/asawicki/nexum/commit/ff075a66d032d49991412bb9311b0c9441894985"
        },
        "date": 1773534070540,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_SimpleCommand",
            "value": 57.51668103841635,
            "unit": "ns",
            "range": "± 0.09281288383361881"
          },
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_3Behaviors",
            "value": 223.1412684747151,
            "unit": "ns",
            "range": "± 1.0694199620555715"
          },
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_SimpleQuery",
            "value": 50.22007894974489,
            "unit": "ns",
            "range": "± 0.026870351058732773"
          },
          {
            "name": "Nexum.Benchmarks.NexumRegressionBenchmarks.Nexum_3NotificationHandlers_Sequential",
            "value": 116.76019266673497,
            "unit": "ns",
            "range": "± 0.18151458436691917"
          }
        ]
      }
    ]
  }
}