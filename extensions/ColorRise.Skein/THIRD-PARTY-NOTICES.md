# Third-party notices

Skein uses the following software, taken from nuget.org as packages and not changed. A game made with Skein ships the runtime ones.

| Software | Used by | License |
|---|---|---|
| [Yarn Spinner](https://github.com/YarnSpinnerTool/YarnSpinner) (`YarnSpinner`) | the runtime, so every game | MIT |
| Yarn Spinner's compiler (`YarnSpinner.Compiler`) | the asset compiler and Game Studio | MIT |
| [Protocol Buffers](https://github.com/protocolbuffers/protobuf) (`Google.Protobuf`), through Yarn Spinner | the runtime | BSD 3-Clause |
| [ANTLR 4](https://github.com/antlr/antlr4) (`Antlr4.Runtime.Standard`), through Yarn Spinner's compiler | the asset compiler and Game Studio | BSD 3-Clause |
| [CsvHelper](https://github.com/JoshClose/CsvHelper), through Yarn Spinner | the runtime | MS-PL or Apache 2.0 |
| `System.Text.Json`, `Microsoft.Extensions.FileSystemGlobbing` ([.NET](https://github.com/dotnet/runtime)), through Yarn Spinner's compiler | the asset compiler and Game Studio | MIT |

Skein's tests run a copy of Yarn Spinner's own test scripts (`tests/ColorRise.Skein.Tests/Conformance/TestCases`), under the same MIT license.

## Yarn Spinner

"Yarn Spinner" is a trademark of Yarn Spinner Pty. Ltd. Skein is not an official Yarn Spinner product.

```
The MIT License (MIT)

Copyright (c) Yarn Spinner Pty. Ltd., Secret Lab Pty. Ltd., and Yarn Spinner contributors.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
