# Contributing

## Pull Requests

We develop using the feature branches, see this section of the Git book:
https://git-scm.com/book/en/v2/Git-Branching-Branching-Workflows.

Please prefix the branch with your user name
(*e.g.,* `mristin/Add-some-feature`).

If you have write permissions to the repository,
create a feature branch directly within the repository.

Otherwise, if you are a non-member contributor, fork the repository and create
the feature branch in your forked repository. See [this Github tuturial](
https://help.github.com/en/github/collaborating-with-issues-and-pull-requests/creating-a-pull-request-from-a-fork
) for more guidance.

### Commit Messages

The commit messages follow the guidelines from
from https://chris.beams.io/posts/git-commit:
* Separate subject from body with a blank line
* Limit the subject line to 50 characters
* Capitalize the subject line
* Do not end the subject line with a period
* Use the imperative mood in the subject line
* Wrap the body at 72 characters
* Use the body to explain *what* and *why* (instead of *how*)

## Development Environment

We use `dotnet` command-line tool for all publishing and continuous integration
tasks. Make sure you have .NET core installed (≥ 3.1.100).

First, change to the `src/` directory. All the subsequent commands should be
invoked from there.

### Build

The solution is built with:

```bash
dotnet publish --configuration Release --output out
```

The resulting binaries are available in `src/out/` directory

### Continuous Integration

You need to restore the tools:

```bash
dotnet tool restore
```

Check the format:

```bash
dotnet format --check
```

Run the tests:

```bash
dotnet test /p:CollectCoverage=true
```

See [src/.github/workflows/check.yml](
src/.github/workflows/check.yml
) for a reproducible setup.

See [src/.github/workflows/generate-nuget.yml](
src/.github/workflows/generate-nuget.yml
) for how to generate and publish a NuGet package.
