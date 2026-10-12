# Release procedure

1. Review the explicit file list in `public-files.json`. Never upload the working directory. Meeting archives, transcripts, original attachments, reference archives, runtime, errors and private documents are excluded.
2. Confirm source and artwork rights. Review each image visually. Search all public files for credentials, personal identifiers, meeting links, private paths and unwanted development references. Do not print suspected secrets.
3. Run core, UI, optional cleanup and static-document checks. Perform a real, consented Teams test, physical DPI/multiple-monitor checks, stop/resume/restart and an extended run. Record untested cases honestly.
4. Prepare an isolated source candidate and portable package; verify the package after extraction, manifest and SHA-256. No archive migration or overwrite of user recordings.
5. Review RU/DE/EN docs, exact version/asset names, license conditions, first-run notice, automatic collection and Windows login startup removal.
6. Obtain the owner's approval for the concrete public contents, rights, repository and release. Publish only this reviewed candidate. No automatic publication in preparation scripts.
7. Download the published package back, compare SHA-256 and test that exact package. Enable Pages from `website/`. Then update the three-language profile catalog using the account's documentation maintenance workflow, without overwriting parallel changes.

Until these conditions are met, do not label the program fully validated. CI success does not establish lawful recording or live Teams reliability.
