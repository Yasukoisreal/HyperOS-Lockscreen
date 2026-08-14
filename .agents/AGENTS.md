# GEMINI.md / AGENTS.md

Behavioral guidelines to optimize Gemini models for software engineering and prevent common LLM coding mistakes. Merge with project-specific instructions (e.g., `.agents/AGENTS.md`) as needed.

**Tradeoff:** These guidelines prioritize correctness, precision, and caution over speed.

---

## 0. Pre-flight Check: Read & Follow Guidelines
**MANDATORY FIRST STEP BEFORE ANY CODE MODIFICATION:**
- You MUST thoroughly review all rules and constraints defined in this `AGENTS.md` file before you begin proposing or executing any code changes.
- Project-specific constraints (especially hardware, OS limitations, and lock screen specifics) must be strictly adhered to in every tool call and generated code block.


## 1. Grounding & Think Before Coding

**Always ground in actual code. Don't assume, don't guess, surface tradeoffs.**

- **Read before writing:** Leverage Gemini's large context window to read full files completely before modifying (`view_file`). Never guess file content, imports, function signatures, or existing patterns.
- **Clarify ambiguities:** If a requirement has multiple valid interpretations, explicitly state them and ask or propose the cleanest option—do not silently pick one.
- **Surface simpler solutions:** If the requested approach is over-engineered or problematic, suggest the simpler, safer alternative.
- **Acknowledge unknowns:** If documentation or code context is missing, investigate or ask rather than hallucinating APIs.

## 2. Simplicity & Minimalist Architecture

**Minimum code that robustly solves the problem. No speculative abstractions.**

- **YAGNI (You Aren't Gonna Need It):** No features, helpers, or config toggles beyond what was explicitly requested.
- **No premature abstractions:** Do not build generic interfaces or utility wrappers for single-use logic.
- **No defensive overkill:** Avoid excessive boilerplate error handling or fallbacks for impossible scenarios in internal code.
- **Conciseness over verbosity:** If a solution can be implemented cleanly in 30 lines, do not generate 150 lines of boilerplate.

> *Ask yourself:* "Would a senior engineer consider this overcomplicated or unnecessarily verbose?" If yes, simplify.

## 3. Surgical & Targeted Changes

**Touch only what you must. Clean up only your own impact.**

When editing existing code:
- **Targeted edits:** Use precise diffs/replacements. Do not reformat or overwrite entire files when changing a few lines.
- **Preserve surrounding code:** Do not "clean up", reformat, or alter existing comments, indentation, or style in unrelated blocks.
- **Match project conventions:** Strictly mirror existing naming, architecture, and coding patterns in the repository.
- **Handle orphans:** Clean up unused imports, variables, or functions that *your* changes made obsolete, but leave pre-existing dead code untouched unless asked.

> *Golden rule:* Every changed line in the diff must trace directly to the user's objective.

## 4. Goal-Driven Execution & Verification

**Define explicit success criteria. Verify before declaring completion.**

- **Transform tasks into verifiable checkpoints:**
  - *"Fix the bug"* → Identify root cause, reproduce/verify failure, apply fix, verify resolution.
  - *"Refactor component"* → Confirm behavior and functionality match exactly before and after.
  - *"Add feature"* → Implement, run build/tests, verify all edge cases.
- **Multi-step execution format:**
  ```text
  1. [Step/Action] → Verify: [Exact check / Command / Expected result]
  2. [Step/Action] → Verify: [Exact check / Command / Expected result]
  ```
- **Self-Review:** Inspect diffs before finalizing to ensure zero unintended side effects or syntax regressions.

## 5. Direct & Concise Communication

- Avoid sycophancy, excessive apologies, or repetitive disclaimers.
- Focus responses on concise explanations of technical decisions, rationale for changes, and actionable verification results.

## 6. Git Version Control & Regular Commits

- **Commit upon completing tasks:** Always create a clear, meaningful Git commit after implementing a feature, refactoring, or fixing a bug once build/verification succeeds.
- **Prevent regressions:** Keep git working tree clean and committed to make rollbacks easy and prevent code loss.

## 7. WP8.1 Project-Specific Constraints (HyperOS Lockscreen)

**Strict Hardware & OS Limitations:**
- **Silverlight Framework:** This project is built using Windows Phone 8.1 Silverlight (NOT WinRT/UWP). Ensure all APIs and UI components (XAML) are compatible with WP8.1 Silverlight.
- **RAM Constraints (512MB Target):** Be extremely careful with memory allocation. The app functions as a Live Lock Screen, which runs in a highly constrained environment.
- **Bitmap Memory Management:** When loading images (especially high-res wallpapers), ALWAYS set `DecodePixelWidth` or `DecodePixelHeight` before setting the image source to prevent OutOfMemory (OOM) crashes. Free image resources aggressively when not in use.

**Lock Screen Specifics:**
- **Performance First:** The lock screen must render instantly when the user wakes up their phone. Minimize synchronous blocking tasks in `OnNavigatedTo` or constructors. Keep the visual tree lightweight and avoid heavy, unoptimized animations.
- **Context Detection:** Rely on `Windows.Phone.System.SystemProtection.ScreenLocked` to detect the context of the app (whether it's running as the actual lock screen or opened normally as a standalone app for settings/customization).

---

**These guidelines are working if:** diffs contain zero unnecessary changes, solutions avoid over-engineering, code is thoroughly grounded via complete file reading, and clarifying questions precede implementation.

