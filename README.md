# dead-csharp

![check-and-publish](
https://github.com/mristin/dead-csharp/workflows/check-and-publish/badge.svg
) [![Coverage Status](
https://coveralls.io/repos/github/mristin/dead-csharp/badge.svg)](
https://coveralls.io/github/mristin/dead-csharp
) [![Nuget](
https://img.shields.io/nuget/v/DeadCsharp)](
https://www.nuget.org/packages/DeadCsharp
)

Dead-csharp detects and removes dead code written in C# comments.

Dead code makes the readability and maintainability of your code base suffer:

* First, commented code is **confusing**: was it commented out on purpose or did
a developer simply forget to uncomment it after debugging?

* Second, commented code is **hard to read**: you have to parse between
the actual code and commented code all the time. Oftentimes, the dead code
is very similar to the actual code.

* Third, commented code makes **diff'ing harder**: since it is often similar
to previous code, dead code in comments will mislead tools such as `diff` and
`git diff`.

Thus dead code is bad for your code base and should be removed.

## Installation

Dead-csharp is available as a dotnet tool.

Either install it globally:

```bash
dotnet tool install -g DeadCsharp
```

or locally (if you use tool manifest, see [this Microsoft tutorial](
https://docs.microsoft.com/en-us/dotnet/core/tools/local-tools-how-to-use)):

```bash
dotnet tool install DeadCsharp
```

## Usage

You run dead-csharp through `dotnet`.

To obtain help:

```bash
dotnet dead-csharp --help
```

You specify which files need to be checked using glob patterns:

```bash
dotnet dead-csharp --inputs "SomeProject/**/*.cs"
```

Multiple patterns are also possible *(we use '\\' for line continuation here)*:

```bash
dotnet dead-csharp \
    --inputs "SomeProject/**/*.cs" \
        "AnotherProject/**/*.cs"
```

If you want to remove the comments with dead code automatically, supply
`--remove`:

```bash
dotnet dead-csharp \
    --inputs "SomeProject/**/*.cs" \
    --remove
```

### Excludes

Sometimes you need to exclude files from check, *e.g.*, when your solution
contains generated code or where you are certain that the dead code is intentional.

You can provide the glob pattern for the files to be excluded with `--excludes`:

```bash
dotnet dead-csharp \
    --inputs "**/*.cs" \
    --excludes --excludes "**/obj/**"
```

If you want to exclude the checks within regions of a source file, you can write
special comments `// dead-csharp on` and `// dead-csharp off`:

```cs
// ...

    // dead-csharp off
    if(something) 
    {
        // deadCode();
    }
    // dead-csharp on

// ...
```

If you want to disable the checks for the whole file, simply write 
`// dead-csharp off` at the begining:

```cs
// dead-csharp off

using System;
// ...
```

Mind that dead-csharp ignores comments starting with `///` by design so you can
freely use code in your structured comments.

## Contributing

Feature requests, bug reports *etc.* are highly welcome! Please [submit
a new issue](https://github.com/mristin/dead-csharp/issues/new).

If you want to contribute in code, please see
[CONTRIBUTING.md](CONTRIBUTING.md).

## Versioning

We follow [Semantic Versioning](http://semver.org/spec/v1.0.0.html).
The version X.Y.Z indicates:

* X is the major version (backward-incompatible w.r.t. detection logic),
* Y is the minor version (backward-compatible), and
* Z is the patch version (backward-compatible bug fix).
