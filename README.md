# MpSoft.StreamCaching

[![Package Version](https://img.shields.io/nuget/v/MpSoft.StreamCaching.svg)](https://www.nuget.org/packages/MpSoft.StreamCaching/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/MpSoft.StreamCaching.svg)](https://www.nuget.org/packages/MpSoft.StreamCaching/)
[![License](https://img.shields.io/github/license/MarekPokornyOva/MpSoft.StreamCaching.svg)](https://github.com/MarekPokornyOva/MpSoft.StreamCaching/blob/master/LICENSE)

### Description
MpSoft.StreamCaching is caching stream supporting ahead reading. That allows to continue reading (and temporary storing) data from source (e.g. network) while processing already read chunk.

### Usage
See RssReader sample.

### Features
* Configurable ahead size
* Configurable block size
* Various temporary storages (disk/memory/custom)
