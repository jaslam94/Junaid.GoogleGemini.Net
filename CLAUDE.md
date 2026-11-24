# Writing style for this repo

This rule applies to all documentation and releases. This includes README.md, ROADMAP.md,
PLAN-*.md, docs/, code doc-comments, commit messages, PR descriptions, and GitHub Release notes.

## Rules

1. Write in ASD STE-100 style (Simplified Technical English). Use short sentences. Use simple,
   direct words. Avoid long noun strings. Avoid complex subordinate clauses. One idea per sentence
   where possible.
2. Use active voice. Say "Google changed the pricing" instead of "the pricing was changed by
   Google."
3. Keep the tone human. Write the way a careful engineer explains something to a colleague. Do not
   sound like a press release or a legal notice.
4. Do not use em dashes (U+2014) anywhere. Use a period, a comma, "and," "but," or two separate
   sentences instead.
5. Prefer short paragraphs. Break up long blocks of text.

## Chat replies

The rules above also apply to chat. In addition:

1. Lead with the answer. Put the conclusion in the first sentence.
2. Do not restate the question, praise it, or open with "Great question."
3. Do not end with a summary that repeats the answer. End on the last new fact or the next step.
4. Use a heading or a list only when the content needs one. A short answer is a short paragraph.
5. Say "I have not verified that" in one sentence when it is true. Do not fill the gap with a guess.
6. When you propose a change, name the file and the exact edit.
7. Keep code, commands, file paths, and error text exact. Do not paraphrase them.

## Why

Em dashes and long, dense sentences are easy for an AI writer to fall into. They are harder to
scan and harder to translate. This rule exists to keep every release note, doc page, PR
description, and chat reply easy to read on the first pass.

## Note for Claude

Check your own draft against these rules before you publish or commit it. This applies to new
writing and to edits of existing files.
