# Nexum

<div class="hero">
  <h1 class="display-2 fw-bold">Nexum</h1>
  <p class="lead">
    A modern CQRS library for <strong>.NET 10 / C# 14</strong>.
    Compile-time safe, zero reflection, <code>ValueTask</code> throughout. A MediatR successor built for
    observability and hot-path performance.
  </p>
  <p class="hero-cta">
    <a class="btn btn-primary btn-lg me-2" href="articles/getting-started.md">Get Started</a>
    <a class="btn btn-outline-primary btn-lg" href="api/index.md">API Reference</a>
  </p>
</div>

## Why Nexum

<div class="row feature-row">
  <div class="col-12 col-md-4">
    <div class="feature-card">
      <h3>ValueTask throughout</h3>
      <p>Every handler returns <code>ValueTask&lt;T&gt;</code>. Synchronous paths allocate zero bytes — no <code>Task</code> heap overhead on the hot path, no state machines for trivial handlers.</p>
    </div>
  </div>
  <div class="col-12 col-md-4">
    <div class="feature-card">
      <h3>Hybrid runtime + generators</h3>
      <p>The Runtime dispatcher works standalone without any Source Generator. Add <code>Nexum.SourceGenerators</code> and you get compile-time discovery, monomorphized pipelines, and Roslyn interceptors — up to 34% faster.</p>
    </div>
  </div>
  <div class="col-12 col-md-4">
    <div class="feature-card">
      <h3>OpenTelemetry built-in</h3>
      <p>Every dispatch creates an <code>Activity</code> with structured tags. Metrics, distributed tracing, and exemplars work out of the box through any OTel-compatible backend.</p>
    </div>
  </div>
</div>

## Core concepts

<div class="row concept-row">
  <div class="col-12 col-md-6 col-lg-4">
    <a class="concept-card" href="articles/getting-started.md">
      <h3>Getting Started</h3>
      <p>Install the package and dispatch your first command.</p>
    </a>
  </div>
  <div class="col-12 col-md-6 col-lg-4">
    <a class="concept-card" href="articles/commands-and-queries.md">
      <h3>Commands &amp; Queries</h3>
      <p>Strict CQRS: <code>ICommand</code>, <code>IQuery</code>, <code>IStreamQuery</code>.</p>
    </a>
  </div>
  <div class="col-12 col-md-6 col-lg-4">
    <a class="concept-card" href="articles/notifications.md">
      <h3>Notifications</h3>
      <p>Domain events with four publish strategies.</p>
    </a>
  </div>
  <div class="col-12 col-md-6 col-lg-4">
    <a class="concept-card" href="articles/behaviors.md">
      <h3>Pipeline Behaviors</h3>
      <p>Separate command/query pipelines, Russian doll model.</p>
    </a>
  </div>
  <div class="col-12 col-md-6 col-lg-4">
    <a class="concept-card" href="articles/streams.md">
      <h3>Stream Queries</h3>
      <p>First-class <code>IAsyncEnumerable&lt;T&gt;</code> support.</p>
    </a>
  </div>
  <div class="col-12 col-md-6 col-lg-4">
    <a class="concept-card" href="articles/source-generators.md">
      <h3>Source Generators</h3>
      <p>Tiered compile-time acceleration: Runtime → Compiled → Interceptors.</p>
    </a>
  </div>
  <div class="col-12 col-md-6 col-lg-4">
    <a class="concept-card" href="articles/dependency-injection.md">
      <h3>Dependency Injection</h3>
      <p><code>AddNexum()</code>, assembly scanning, handler lifetimes.</p>
    </a>
  </div>
  <div class="col-12 col-md-6 col-lg-4">
    <a class="concept-card" href="articles/opentelemetry.md">
      <h3>Observability</h3>
      <p>OpenTelemetry tracing and metrics out of the box.</p>
    </a>
  </div>
  <div class="col-12 col-md-6 col-lg-4">
    <a class="concept-card" href="articles/results.md">
      <h3>Result Pattern</h3>
      <p><code>Result&lt;T, TError&gt;</code> with FluentValidation support.</p>
    </a>
  </div>
  <div class="col-12 col-md-6 col-lg-4">
    <a class="concept-card" href="articles/aspnetcore-integration.md">
      <h3>ASP.NET Core</h3>
      <p>Minimal API endpoints, middleware, Problem Details.</p>
    </a>
  </div>
  <div class="col-12 col-md-6 col-lg-4">
    <a class="concept-card" href="articles/testing.md">
      <h3>Testing</h3>
      <p><code>NexumTestHost</code>, fake dispatchers, behavior isolation.</p>
    </a>
  </div>
  <div class="col-12 col-md-6 col-lg-4">
    <a class="concept-card" href="articles/migration-from-mediatr.md">
      <h3>Migration from MediatR</h3>
      <p>Gradual migration via adapters and Roslyn analyzer.</p>
    </a>
  </div>
</div>

## Benchmarks

<div class="row stats-row">
  <div class="col-6 col-md-3">
    <div class="stat">
      <span class="stat-value">18.96 ns</span>
      <span>command dispatch</span>
    </div>
  </div>
  <div class="col-6 col-md-3">
    <div class="stat">
      <span class="stat-value">0 B</span>
      <span>hot path allocations</span>
    </div>
  </div>
  <div class="col-6 col-md-3">
    <div class="stat">
      <span class="stat-value">2.1×</span>
      <span>faster than MediatR</span>
    </div>
  </div>
  <div class="col-6 col-md-3">
    <div class="stat">
      <span class="stat-value">28×</span>
      <span>less memory (notifications)</span>
    </div>
  </div>
</div>

## About

<p class="about-line">
  Nexum is developed by <a href="https://github.com/Wizard-Software">Wizard-Software</a> and hosted on GitHub at
  <a href="https://github.com/Wizard-Software/nexum">Wizard-Software/nexum</a>. MIT licensed.
</p>
