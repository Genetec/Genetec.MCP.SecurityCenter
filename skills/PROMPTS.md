# What to ask for

Describe your task in plain language. A client that supports skills can load the relevant
instructions without requiring you to name them.

## Entity resolver

> Find the camera covering the loading dock.

> How many doors are there on the second floor of building A?

> Is there more than one entity called Main Entrance?

The `sc-entity-resolver` skill resolves names to GUIDs, asks you to choose when a match is
ambiguous, and reads verified configuration fields without pulling complete records.

## PII guard

> What did Alex Rivera do at the loading dock between 8 and 10 last night?

> For these three cardholders, which access groups grant them entry?

The `sc-pii-guard` skill guides scoped reads and answers, including names or bounded lists
when the request needs them. Stored passwords, PINs, tokens, and raw credential values
remain restricted. It does not replace Security Center privileges or certify files;
apply the same guidance to inspectable artifact content and mark uninspectable content
**Unverified**.

## Using both

> Find the Main Entrance door and tell me how many access denials occurred there yesterday,
> without naming cardholders or exposing credential values.

Entity resolver identifies the door. PII guard keeps retrieval within scope and reviews
the answer against the request. Ambiguous entity matches are presented for you to choose.
