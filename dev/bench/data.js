window.BENCHMARK_DATA = {
  "lastUpdate": 1771117543639,
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
      }
    ]
  }
}