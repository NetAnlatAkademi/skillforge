# The model runner

Everything else in SkillForge runs locally and sends nothing anywhere. This is the one exception, and it exists because
one question cannot be answered any other way: **would an agent actually choose this skill for this request?**

It is opt-in, per run, by flags. There is no default endpoint and no default model. A run that does not name a model
makes no network call, and `model_activation` cases are reported as skipped with a line saying why.

```bash
# a local model
skillforge eval ./my-skill --model qwen3:8b --model-endpoint http://localhost:11434/v1

# a hosted one
export OPENAI_API_KEY=...
skillforge eval ./my-skill --model gpt-5 --model-endpoint https://api.openai.com/v1 \
  --model-api-key-env OPENAI_API_KEY
```

## You pick the model

The transport is OpenAI's `/chat/completions`, which is what Ollama, LM Studio, llama.cpp's server, vLLM, OpenRouter,
Azure OpenAI and OpenAI itself all speak. One adapter therefore reaches a model on your laptop and a hosted frontier
model alike, and SkillForge does not have to bless a vendor to be useful. A provider with a different API shape gets its
own adapter beside this one, the same way the MCP readers work.

## The API key is a variable name, never a value

`--model-api-key-env` takes the **name** of an environment variable. SkillForge reads the value once, puts it in a
request header, and holds it nowhere else:

- no field on `ModelSettings`, `ModelIdentity`, or any report type could hold a key
- the key is never an argument, so it cannot land in a shell history or a CI log
- a named-but-unset variable fails before any request, because "your endpoint rejected you" is the wrong thing to tell
  somebody whose shell simply has no key in it

## What it asks, and why the answer means something

Two design choices carry the honesty here.

**Distractors.** The skill is offered to the model alongside its siblings — the other skills installed next to it. Asked
about a single skill in isolation, a model says yes to almost anything, so a probe without competition measures the
model's agreeableness rather than the skill's description. A skill with no siblings still gets probed, and the report
says outright that the result is weak evidence.

**Repetition.** Each prompt is asked `runs` times at `temperature: 0`. Zero temperature reduces variation; it does not
remove it. One answer is an anecdote, so the report is `k` of `n` — always, even when it passes, because 8 of 10 and 10
of 10 both clear a 0.8 threshold and the difference is most of what an author needs to know.

```yaml
cases:
  - name: a model routes an API review request here
    model_activation:
      should_fire:
        - review my ASP.NET Core controller before release
      should_not_fire:
        - translate this paragraph into Turkish
      runs: 3
      threshold: 0.67
```

`should_not_fire` matters more than it looks. A suite of positives alone measures nothing: it is easy to write a
description that a model picks for everything.

`model_activation` is a **separate key** from `activation` rather than an extension of it. `activation` is published and
means vocabulary overlap; widening it would change what an existing eval file asserts without its author touching it.
The name also says where the answer comes from, which a reader should never have to guess.

## It is kept apart from the deterministic findings

A model's answer gets **no `SFxxxx` code** and appears in its own section, never among the eval cases. An `SFxxxx` code
means a fact somebody can go and see in a file; a model's answer is a sample from a distribution. Giving them the same
shape would invite them to be read the same way — including into SARIF, where a generated claim would arrive as a
finding about source code that nobody can verify by looking.

The section always names the model and the endpoint, and reports request and token counts. A rate without the model
beside it is a rumour: "7 of 10" means something different for a 4B local model than for a frontier one.

A case whose only assertion is `model_activation` is reported as **skipped** when no model ran — not passed. That
distinction was a real bug during development: the case sailed through as `ok` because the deterministic evaluator had
nothing to check, which is precisely the lie this feature must not tell.

## Guards

- `--max-model-requests` (default 100) refuses a run before making any request. Ten prompts at ten runs is a hundred
  requests, and somebody should learn that before paying for it.
- `--model` and `--model-endpoint` must be given together; one without the other is a parse error, because `--model`
  alone would look like it worked and probe nothing.
- An unreachable or refusing endpoint fails the run as a **usage error** with the endpoint named and the underlying
  reason quoted. It is never reported as "the skill did not fire" — that would be a lie about the skill dressed as
  evidence.

## What it still is not

A model answering a routing question about a list of skills is not an agent mid-conversation with a toolbox, a system
prompt and a task. This is much closer to activation testing than word overlap, and it is still a sample of one model's
decision rather than a guarantee. The report says so, in those words, every time.

There is no response cache. Caching would be the obvious way to make re-runs free, and it is at odds with the point:
the number is a sample over `n` runs, and serving a stored answer would make the rate a fiction. If a cache arrives it
has to cache a whole probe, with its run count and its model, or nothing.
