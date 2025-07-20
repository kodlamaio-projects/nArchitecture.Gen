[![GitHub contributors](https://img.shields.io/github/contributors/kodlamaio-projects/nArchitecture.Gen.svg?style=for-the-badge)](https://github.com/kodlamaio-projects/nArchitecture.Gen/graphs/contributors)
[![GitHub forks](https://img.shields.io/github/forks/kodlamaio-projects/nArchitecture.Gen.svg?style=for-the-badge)](https://github.com/kodlamaio-projects/nArchitecture.Gen/network/members)
[![GitHub stars](https://img.shields.io/github/stars/kodlamaio-projects/nArchitecture.Gen.svg?style=for-the-badge)](https://github.com/kodlamaio-projects/nArchitecture.Gen/stargazers)
[![GitHub issues](https://img.shields.io/github/issues/kodlamaio-projects/nArchitecture.Gen.svg?style=for-the-badge)](https://github.com/kodlamaio-projects/nArchitecture.Gen/issues)
[![GitHub license](https://img.shields.io/github/license/kodlamaio-projects/nArchitecture.Gen.svg?style=for-the-badge)](https://github.com/kodlamaio-projects/nArchitecture.Gen/blob/master/LICENSE)

[![nArchitecture](https://github.com/user-attachments/assets/d1e70b04-06d8-4add-a58f-7002d1abfd58)](https://github.com/kodlamaio-projects/nArchitecture.Gen)

# nArchitecture Project Code Generator Tool

[Report Bug](https://github.com/kodlamaio-projects/nArchitecture.Gen/issues) ·
[Request Feature](https://github.com/kodlamaio-projects/nArchitecture.Gen/discussions)

## 💻 About The Project

As Kodlama.io, we have chosen to unveil examples of finalized projects. Natively integrated with Clean Architecture principles, nArchitecture CLI tool epitomizes cutting-edge development methodologies. This monolithic project incorporates Clean Architecture, CQRS, Advanced Repository patterns, Dynamic Querying capabilities, JWT and OTP authentication mechanisms, Google & Microsoft Auth integration, Role-Based Management systems, Distributed Caching powered by Redis, Logging functionalities leveraging Serilog, Elastic Search functionalities, and a feature-rich Code Generator. By actively contributing, you not only bolster the project but also acquire invaluable insights and expertise.

### Built With

[![](https://img.shields.io/badge/.NET%20Core-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://learn.microsoft.com/tr-tr/dotnet/welcome)

## ⚙️ Getting Started

To get a local copy up and running follow these simple steps.

### Prerequisites

- .NET 8

### Installation

1. Install the tool globally:
   ```sh
   dotnet tool install --global NArchitecture.Gen
   ```
   

   You can also install the tool locally in the project:
   1. Create dotnet tool manifest:
   ```sh
   dotnet new tool-manifest
   ```
   2. Install the tool locally:
   ```sh
   dotnet tool install NArchitecture.Gen
   ```
## 🚀 Usage

### Quick Start

1. Run `nArchGen` command in project solution directory.

### Template System v2.0

nArchitecture.Gen v2.0 introduces a comprehensive template selection system:

#### List Available Templates
```sh
nArchGen templates
```

#### Create New Project
```sh
# Create with default template (interactive selection)
nArchGen new MyProject

# Create with specific template
nArchGen new MyProject --template minimal
```

#### Code Generation
```sh
# Generate CRUD operations
nArchGen generate crud User BaseDbContext

# Generate custom command
nArchGen generate command SyncUser Users

# Generate custom query
nArchGen generate query GetUserByEmail Users
```

For detailed template system documentation, see [Template System Guide](./docs/TemplateSystem.md).

## 🚧 Roadmap

See the [open issues](https://github.com/kodlamaio-projects/nArchitecture.Gen/issues) for a list of proposed features (and known issues).

## 🤝 Contributing

Contributions are what make the open source community such an amazing place to be learn, inspire, and create. Any contributions you make are **greatly appreciated**.

1. Fork the project and clone your local machine
2. Create your Feature Branch:
    ```sh 
    git checkout -b <Feature>/<AmazingFeature>
    ```
3. Develop
4. Commit your Changes:
    ```sh
    git add . && git commit -m '<SemanticCommitType>(<Scope>): <AmazingFeature>'
    ```
   💡 Check [Semantic Commit Messages](./docs/Semantic%20Commit%20Messages.md).

   💡 You can also use [Commitizen CLI](https://github.com/commitizen/cz-cli).
   
5. Push to the Branch:
   ```sh
   git push origin <Feature>/<AmazingFeature>
   ```
6. Open a Pull Request

### Analysis

1. If not, Install dotnet tool `dotnet tool restore`.
2. Run anaylsis command `dotnet roslynator analyze`

### Format

1. If not, Install dotnet tool `dotnet tool restore`.
2. Run format command `dotnet csharpier .`

## ⚖️ License

Distributed under the MIT License. See `LICENSE` for more information.

## 📧 Contact

**Project Link:** [https://github.com/kodlamaio-projects/nArchitecture.Gen](https://github.com/kodlamaio-projects/nArchitecture.Gen)
