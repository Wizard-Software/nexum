window.BENCHMARK_DATA = {
  "lastUpdate": 1771104685712,
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
      }
    ]
  }
}