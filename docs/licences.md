# Libraries and their licences

Rule R342 of docs/rules.md. Every package the solution references, every package a planned project of docs/addin-design.md will reference and every requirement of requirements.txt is listed here with its version, its licence as the package metadata gives it, where it is used and how that was checked. A package that ships with the add-in or its worker has an MIT, BSD-2-Clause, BSD-3-Clause or Apache-2.0 licence, nothing else (Round ADDIN, Bader). python/tests/test_licences.py holds this file against every csproj and against requirements.txt.

How each licence is known:

- READ: the .nuspec of the package in the local NuGet cache, `%USERPROFILE%\.nuget\packages\ID\VERSION\ID.nuspec`, its license element, or the licence file inside the package where the nuspec gives only a file or a URL.
- SEARCHED: the .nuspec on nuget.org, `https://api.nuget.org/v3-flatcontainer/ID/VERSION/ID.nuspec`, for a package not in the cache, and the licence URL a nuspec names.
- RAN: the library loaded and used in a .NET Framework 4.8 process, see Load check below.
- For Python: the METADATA of the installed package in .venv\Lib\site-packages, License-Expression, else License, else the licence classifier.

## .NET packages

Ships with says where the DLL goes: add-in (loaded inside Revit), worker (CadReader.exe, a process of its own beside Revit, docs/addin-design.md section 2), tools (the reader, the placer, the test host and the two watchers of today), build only (reference assemblies, nothing copied) or tests only.

| Package | Version | Licence | Ships with | Asset a net48 project takes | Checked |
|---|---|---|---|---|---|
| ACadSharp | 3.8.0 | MIT | worker, tools | lib/net48, needs System.Memory 4.6.3 | READ acadsharp.nuspec, RAN |
| Newtonsoft.Json | 13.0.1 | MIT | worker, tools. The add-in ships no copy: Common pins 13.0.1 since Phase 4a (R346) and builds against it, and inside Revit the copy of Revit, 13.0.1, serves Common and the add-in (below). 13.0.3 until Phase 4a | lib/net45, lib/netstandard2.0 for Common | READ newtonsoft.json.nuspec of 13.0.1, RAN |
| PdfPig | 0.1.16 | Apache-2.0 | worker, planned for Phase 4a | lib/net471, needs Microsoft.Bcl.HashCode 6.0.0 and System.Memory 4.6.0 | SEARCHED https://api.nuget.org/v3-flatcontainer/pdfpig/0.1.16/pdfpig.nuspec, RAN |
| DocumentFormat.OpenXml | 3.5.1 | MIT | worker, planned for Phase 4a | lib/net46, needs DocumentFormat.OpenXml.Framework 3.5.1 | SEARCHED https://api.nuget.org/v3-flatcontainer/documentformat.openxml/3.5.1/documentformat.openxml.nuspec, RAN |
| DocumentFormat.OpenXml.Framework | 3.5.1 | MIT | worker, planned for Phase 4a | lib/net46, needs only System, System.Xml and WindowsBase of the framework | SEARCHED https://api.nuget.org/v3-flatcontainer/documentformat.openxml.framework/3.5.1/documentformat.openxml.framework.nuspec, RAN |
| Microsoft.Bcl.HashCode | 6.0.0 | MIT | worker, by PdfPig, planned for Phase 4a | lib/net462 | READ microsoft.bcl.hashcode.nuspec, restored by the load check |
| System.Memory | 4.6.3 | MIT | worker, tools, by ACadSharp and PdfPig | lib/net462 | READ system.memory.nuspec |
| System.Buffers | 4.6.1 | MIT | worker, tools, by System.Memory | lib/net462 | READ system.buffers.nuspec |
| System.Numerics.Vectors | 4.6.1 | MIT | worker, tools, by System.Memory | lib/net462 | READ system.numerics.vectors.nuspec |
| System.Runtime.CompilerServices.Unsafe | 6.1.2 | MIT | worker, tools, by System.Memory | lib/net462 | READ system.runtime.compilerservices.unsafe.nuspec |
| Nice3point.Revit.Api.RevitAPI | 2024.3.60 | MIT | build only, ExcludeAssets runtime, Revit loads its own RevitAPI.dll | ref | READ LICENSE.md in the package, the MIT text |
| Nice3point.Revit.Api.RevitAPIUI | 2024.3.60 | MIT | build only, ExcludeAssets runtime | ref | READ LICENSE.md in the package, the MIT text |
| Microsoft.NETFramework.ReferenceAssemblies | 1.0.3 | MIT | build only | build | READ the nuspec licenseUrl, SEARCHED https://raw.githubusercontent.com/Microsoft/dotnet/master/LICENSE, The MIT License |
| Microsoft.NETFramework.ReferenceAssemblies.net48 | 1.0.3 | MIT | build only, by the package above | build | READ the nuspec licenseUrl, the same MIT file |
| Microsoft.NETFramework.ReferenceAssemblies.net461 | 1.0.3 | MIT | build only, pulled in by the restore of SiteAdaptation.C3DProbe.Civil, a project under dotnet outside the sln, and since Phase 4a by the netstandard2.0 target of Common | build | READ the nuspec licenseUrl, the same MIT file, and obj\project.assets.json of both projects |
| NETStandard.Library | 2.0.3 | MIT | build only, the reference assemblies the SDK gives the netstandard2.0 target of Common (R346) | build | READ LICENSE.TXT in the package, The MIT License (MIT) |
| Microsoft.NETCore.Platforms | 3.1.0 | MIT | build only, PrivateAssets all in Common: the runtime graph of the restore, no file copied, in place of the 1.1.0 NETStandard.Library asks for, whose licence is the Microsoft .NET Library licence (READ dotnet_library_license.txt in the 1.1.0 package, Q079) | none, lib/netstandard1.0 holds only _._ | READ microsoft.netcore.platforms.nuspec of 3.1.0 |
| Microsoft.NET.Test.Sdk | 17.11.1 | MIT | tests only | lib/net462 | READ microsoft.net.test.sdk.nuspec |
| Microsoft.CodeCoverage | 17.11.1 | MIT | tests only, by Microsoft.NET.Test.Sdk | lib/net462 | READ microsoft.codecoverage.nuspec |
| Microsoft.TestPlatform.ObjectModel | 17.11.1, 17.13.0 | MIT | tests only, 17.13.0 on net48 and 17.11.1 by the test host of the net8.0 leg | lib/net462, lib/netcoreapp3.1 | READ microsoft.testplatform.objectmodel.nuspec of both |
| Microsoft.TestPlatform.TestHost | 17.11.1 | MIT | tests only, the net8.0 leg (R346) | lib/netcoreapp3.1 | READ microsoft.testplatform.testhost.nuspec |
| System.Collections.Immutable | 1.5.0 | MIT | tests only | lib/netstandard2.0 | READ LICENSE.TXT in the package, The MIT License |
| System.Reflection.Metadata | 1.6.0 | MIT | tests only | lib/netstandard2.0 | READ LICENSE.TXT in the package, The MIT License |
| xunit | 2.9.3 | Apache-2.0 | tests only | none, a meta package | READ xunit.nuspec |
| xunit.abstractions | 2.0.3 | Apache-2.0 | tests only | lib/net35 | READ the nuspec licenseUrl, SEARCHED https://raw.githubusercontent.com/xunit/xunit/master/license.txt, Apache License 2.0, the asset by obj\project.assets.json of SiteAdaptation.Tests |
| xunit.analyzers | 1.18.0 | Apache-2.0 | tests only, an analyzer | none | READ xunit.analyzers.nuspec |
| xunit.assert | 2.9.3 | Apache-2.0 | tests only | lib/netstandard1.1 | READ xunit.assert.nuspec |
| xunit.core | 2.9.3 | Apache-2.0 | tests only | none, a meta package | READ xunit.core.nuspec |
| xunit.extensibility.core | 2.9.3 | Apache-2.0 | tests only | lib/net452 | READ xunit.extensibility.core.nuspec |
| xunit.extensibility.execution | 2.9.3 | Apache-2.0 | tests only | lib/net452 | READ xunit.extensibility.execution.nuspec |
| xunit.runner.visualstudio | 3.1.5 | Apache-2.0 | tests only | lib/net472 | READ xunit.runner.visualstudio.nuspec |

The package list comes from the obj/project.assets.json of the projects under dotnet (RAN): the nine projects of dotnet/SiteAdaptation.sln hold 23 packages, and SiteAdaptation.C3DProbe.Civil, outside the sln, adds Microsoft.NETFramework.ReferenceAssemblies.net461, 24 in all, then the restore of the load check gives the four planned ones, 28 rows. Since Phase 4a (R346) the sln holds 27 packages, one of them at two versions: the netstandard2.0 target of Common adds NETStandard.Library, Microsoft.NETCore.Platforms and the net461 reference assemblies, the net8.0 leg of the tests Microsoft.TestPlatform.TestHost and ObjectModel 17.11.1, 31 rows with the four planned ones. A row of two versions lists both, parted by a comma. python/tests/test_licences.py holds every package of every assets file under dotnet against its row and its version, so a package a restore pulls in is never left out. PdfPig asks for System.Memory 4.6.0 and ACadSharp for 4.6.3, NuGet takes 4.6.3 for the worker, so one System.Memory ships (RAN, the assets file of the load check).

Not chosen, with the reason:
- ClosedXML 0.105.1, MIT, but it pulls ClosedXML.Parser, ExcelNumberFormat, RBush.Signed, SixLabors.Fonts (1.0.0 Apache-2.0, 2.x a licence file) and System.Memory 4.5.5 into the worker, for two files that need no font measure (READ the cached closedxml.nuspec and sixlabors.fonts 1.0.0 nuspec).
- EPPlus 8.7.2 gives a licence file, not an expression (SEARCHED https://api.nuget.org/v3-flatcontainer/epplus/8.7.2/epplus.nuspec), the file was not read, so it is not used.
- iTextSharp 5.5.13.6 names the AGPL (SEARCHED https://api.nuget.org/v3-flatcontainer/itextsharp/5.5.13.6/itextsharp.nuspec). Revit 2024 ships itextsharp.dll 4.1.6.0 in its own folder, the add-in never references it.

## Load check in a .NET Framework 4.8 process

RAN on 2026-10-26, outside the repo in the scratchpad of the session (libcheck): a net48 host that stands in for Revit 2024 holds in its own folder System.Memory 4.0.1.1, System.Buffers 4.0.3.0, System.Numerics.Vectors 4.1.4.0, System.Runtime.CompilerServices.Unsafe 4.0.4.1, Newtonsoft.Json 13.0.1 and DocumentFormat.OpenXml 2.16.0, uses them first, has no binding redirect for them, then loads a net48 add-in by LoadFrom from another folder. The add-in writes and reads back a DXF with ACadSharp 3.8.0, a PDF with PdfPig 0.1.16, an xlsx with DocumentFormat.OpenXml 3.5.1 and a JSON text with Newtonsoft.Json.

| Library | With no resolver | With a resolver that serves a file of the add-in folder of the same or a higher version, and never a request of another assembly |
|---|---|---|
| ACadSharp 3.8.0 | ok, 1 line read back, System.Memory 4.0.5.0 from the add-in folder beside the 4.0.1.1 of the host | ok |
| PdfPig 0.1.16 | failed, FileNotFoundException for System.Memory 4.0.2.0, the add-in holds 4.0.5.0 | ok, 8 letters read back, System.Memory 4.0.2.0 and System.Buffers 4.0.4.0 served 4.0.5.0 |
| DocumentFormat.OpenXml 3.5.1 | ok, 3.5.1.0 beside the 2.16.0.0 of the host | ok |
| Newtonsoft.Json 13.0.3 | ok, the host copy served, file 13.0.1.25517 | ok, the same |

A worker process needs no resolver. The same probe built as a net48 exe with the same four packages got a generated exe.config that redirects System.Memory and System.Buffers 0.0.0.0 to 4.0.5.0 onto 4.0.5.0, and all four libraries ran, Newtonsoft.Json from its own folder, file 13.0.3.27908 (RAN). CadReader.exe.config holds the startup line only today, since its references agree. docs/addin-design.md keeps ACadSharp, PdfPig and DocumentFormat.OpenXml in the worker and out of the Revit process, so inside Revit a resolver is needed only for the assemblies of the tool itself.

## What Revit 2024 ships

READ in C:\Program Files\Autodesk\Revit 2024, the assembly version of each file by its metadata:

| File in the Revit 2024 folder | Assembly version | File version | What the add-in does |
|---|---|---|---|
| NewtonSoft.Json.dll | 13.0.0.0 | 13.0.1.25517 | Revit.exe.config redirects 0.0.0.0 to 13.0.0.0 onto 13.0.0.0, so this copy serves every add-in that asks for 13.0.x. Common pins Newtonsoft.Json 13.0.1 since Phase 4a (R346), so the tests run the code Revit runs, and the add-in ships no copy of its own |
| System.Memory.dll | 4.0.1.1 | 4.6.28619.01 | No redirect. Never asked for inside Revit, the readers that need 4.0.5.0 run in the worker |
| System.Buffers.dll | 4.0.3.0 | 4.6.28619.01 | the same |
| System.Numerics.Vectors.dll | 4.1.4.0 | 4.6.26515.06 | the same |
| System.Runtime.CompilerServices.Unsafe.DLL | 4.0.4.1 | 4.6.28619.01 | the same |
| AddIns\DynamoForRevit\DocumentFormat.OpenXml.dll | 2.12.3.0 | 2.12.3 | Another version, loads side by side as 2.16.0 did in the load check. Never asked for inside Revit, the worker writes the two Excel files |
| itextsharp.dll | 4.1.6.0 | 4.1.6.0 | Not used |

The add-ins of AddIns\ParametersServiceDB, PnIDModeler and DynamoForRevit carry copies of their own (Newtonsoft.Json 12.0.0.0 and 13.0.0.0, System.Runtime.CompilerServices.Unsafe 4.0.4.1, 5.0.0.0 and 6.0.0.0) (READ). %APPDATA%\Autodesk\Revit\Addins\2024 holds the Newtonsoft.Json 13.0.3 of the reader, the placer and the watcher of this tool, installed before Phase 4a (READ). No DocumentFormat.OpenXml other than Dynamo's, no PdfPig and no ACadSharp is under the Revit folder or the two add-in folders (READ).

## Python packages

Python stays in the repo as the checker and the data book builder only, it never runs on the user's PC and nothing of it ships with the add-in. The licence is the METADATA of the package installed in .venv, the test reads it again on every run.

| Package | Version | Licence | Needed by | Checked |
|---|---|---|---|---|
| pdfplumber | 0.11.7 | MIT License | requirements.txt | READ .venv\Lib\site-packages\pdfplumber-0.11.7.dist-info\METADATA, the classifier |
| openpyxl | 3.1.5 | MIT | requirements.txt | READ openpyxl-3.1.5.dist-info\METADATA |
| pytest | 8.4.2 | MIT | requirements.txt | READ pytest-8.4.2.dist-info\METADATA |
| shapely | 2.2.0 | BSD-3-Clause | requirements.txt | READ shapely-2.2.0.dist-info\METADATA, the wheel also bundles GEOS under LGPLv2.1, licenses\LICENSE_GEOS (Q076) |
| numpy | 2.5.4 | BSD-3-Clause AND 0BSD AND MIT AND Zlib AND CC0-1.0 | requirements.txt | READ numpy-2.5.4.dist-info\METADATA |
| pdfminer.six | 20250506 | MIT | pdfplumber | READ pdfminer_six-20250506.dist-info\METADATA |
| pillow | 13.0.0 | MIT-CMU | pdfplumber | READ pillow-13.0.0.dist-info\METADATA |
| pypdfium2 | 5.14.0 | BSD-3-Clause, Apache-2.0, dependency licenses | pdfplumber | READ pypdfium2-5.14.0.dist-info\METADATA |
| charset-normalizer | 3.5.2 | MIT | pdfminer.six | READ METADATA |
| cryptography | 50.0.2 | Apache-2.0 OR BSD-3-Clause | pdfminer.six | READ METADATA |
| cffi | 2.1.1 | MIT-0 | cryptography | READ METADATA |
| pycparser | 3.11 | BSD-3-Clause | cffi | READ METADATA |
| et_xmlfile | 2.0.0 | MIT | openpyxl | READ METADATA |
| iniconfig | 2.3.1 | MIT | pytest | READ METADATA |
| packaging | 26.3 | Apache-2.0 OR BSD-2-Clause | pytest | READ METADATA |
| pluggy | 1.7.0 | MIT | pytest | READ METADATA |
| Pygments | 2.21.0 | BSD-2-Clause | pytest | READ METADATA |
| colorama | 0.4.6 | BSD License | pytest on Windows | READ METADATA, the classifier |

## Conflicts

- shapely 2.2.0 is BSD-3-Clause, and its Windows and Linux wheels bundle the GEOS library under LGPLv2.1 (READ licenses\LICENSE_GEOS). numpy, pillow and pypdfium2 hold parts under other permissive licences (0BSD, Zlib, CC0-1.0, MIT-CMU and the dependency licences of PDFium). None of them ships with the add-in. Q076 asks Bader whether the Python checker keeps them.
