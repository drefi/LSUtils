# 📋 Plano de Organização - LSUtils

**Data de Criação:** 29 de Dezembro de 2025  
**Status:** 🏗️ Fase 2 - Reorganização de Código (95% completo)  
**Versão:** 1.0
**Última Atualização:** 29 de Dezembro de 2025

---

## 📑 Índice

1. [Visão Geral](#visão-geral)
2. [Estrutura Atual](#estrutura-atual)
3. [Problemas Identificados](#problemas-identificados)
4. [Estrutura Proposta](#estrutura-proposta)
5. [Plano de Ação](#plano-de-ação)
6. [Documentação](#documentação)
7. [Testes](#testes)
8. [CI/CD e Qualidade](#cicd-e-qualidade)

---

## 🎯 Visão Geral

### Objetivo

Organizar o projeto LSUtils em uma biblioteca .NET modular, bem documentada e testada, seguindo as melhores práticas de desenvolvimento.

### Escopo

- Reorganizar estrutura de arquivos e pastas
- Padronizar namespaces e nomenclaturas
- Criar documentação completa
- Reorganizar e expandir testes
- Configurar build e CI/CD
- Definir versionamento e publicação

### Princípios

- ✅ **Modularidade**: Componentes independentes e reutilizáveis
- ✅ **Clareza**: Código e documentação autoexplicativos
- ✅ **Testabilidade**: Cobertura mínima de 80%
- ✅ **Manutenibilidade**: Fácil de entender e modificar
- ✅ **Performance**: Otimizado para casos de uso reais

---

## 📊 Estrutura Atual

### Diretórios

```
LSUtils/
├── src/
│   ├── *.cs (16 arquivos na raiz)
│   ├── Collections/ (3 arquivos)
│   ├── Exceptions/ (5 arquivos)
│   ├── Graphs/ (8 arquivos)
│   ├── Hex/ (1 arquivo)
│   ├── JsonConverters/ (7 arquivos)
│   ├── Locale/ (2 arquivos)
│   ├── Logging/ (5 arquivos + docs)
│   ├── ProcessSystem/ (20 arquivos + docs)
│   └── Tests/ (9 arquivos)
├── bin/
├── obj/
├── LSUtils.csproj
└── MIGRATION_PLAN.md
```

### Categorias de Componentes

#### Core (Raiz do src/)

- `ILSClass.cs`, `ILSContext.cs`, `ILSSerializable.cs`, etc.
- `LSAction.cs`, `LSActionT.cs`, `LSActionT2.cs`
- `LSExtensionHelpers.cs`, `LSMath.cs`, `LSSemaphore.cs`
- `LSSignals.cs`, `LSTick.cs`, `LSTimestamp.cs`, `LSVersion.cs`
- `Random.cs`

#### Subsistemas

- **Collections**: Estruturas de dados especializadas
- **Exceptions**: Exceções customizadas
- **Graphs**: Sistema de grafos e pathfinding
- **Hex**: Coordenadas hexagonais
- **JsonConverters**: Conversores JSON customizados
- **Locale**: Sistema de localização
- **Logging**: Sistema de logging
- **ProcessSystem**: Sistema de processos/behaviour trees

---

## ⚠️ Problemas Identificados

### 1. Estrutura Organizacional

- ❌ Muitos arquivos na raiz do `src/`
- ❌ Falta de agrupamento lógico por funcionalidade
- ❌ Mistura de conceitos diferentes no mesmo nível
- ❌ Testes misturados com código de produção

### 2. Namespaces

- ⚠️ Inconsistência: `LSUtils.Lehmer` vs `LSUtils.ProcessSystem`
- ⚠️ Namespaces não refletem estrutura de pastas
- ⚠️ Falta de namespace raiz consistente

### 3. Documentação

- ❌ Sem README.md principal
- ⚠️ Documentação apenas em subsistemas específicos (Logging, ProcessSystem)
- ❌ Falta de exemplos de uso
- ❌ Falta de guia de contribuição
- ❌ Falta de changelog

### 4. Testes

- ❌ Testes misturados em `src/Tests/` junto com código
- ❌ Foco apenas em ProcessSystem
- ❌ Falta de testes para outros componentes
- ⚠️ Sem relatórios de cobertura configurados

### 5. Build e CI/CD

- ❌ Sem configuração de CI/CD
- ❌ Sem pipeline de testes automáticos
- ❌ Sem versionamento semântico configurado
- ❌ Sem processo de publicação (NuGet)

### 6. Configuração

- ⚠️ `IsTestProject=true` no projeto principal
- ⚠️ `GenerateAssemblyInfo=false` - sem informações de versão
- ❌ Falta de arquivo `.editorconfig` na raiz
- ❌ Falta de configuração de análise estática

---

## 🏗️ Estrutura Proposta

### Hierarquia de Diretórios

```tree
LSUtils/
├── docs/
│   ├── README.md (índice principal)
│   ├── getting-started.md
│   ├── api-reference/
│   │   ├── core.md
│   │   ├── collections.md
│   │   ├── graphs.md
│   │   ├── process-system.md
│   │   └── logging.md
│   ├── guides/
│   │   ├── process-system-guide.md
│   │   ├── logging-guide.md
│   │   └── graph-guide.md
│   └── examples/
│       ├── process-system-examples.md
│       ├── logging-examples.md
│       └── graph-examples.md
├── samples/
│   ├── ProcessSystem.Samples/
│   ├── Logging.Samples/
│   └── Graphs.Samples/
├── src/
│   └── LSUtils/
│       ├── Core/
│       │   ├── Interfaces/
│       │   │   ├── ILSClass.cs
│       │   │   ├── ILSContext.cs
│       │   │   ├── ILSSerializable.cs
│       │   │   ├── ILSSerializer.cs
│       │   │   └── ILSState.cs
│       │   ├── Types/
│       │   │   ├── LSVersion.cs
│       │   │   ├── LSTimestamp.cs
│       │   │   ├── LSTick.cs
│       │   │   └── LSSerializerInfo.cs
│       │   ├── Math/
│       │   │   ├── LSMath.cs
│       │   │   ├── ILSVector2.cs
│       │   │   └── ILSVector2I.cs
│       │   ├── Delegates/
│       │   │   ├── LSAction.cs
│       │   │   ├── LSActionT.cs
│       │   │   └── LSActionT2.cs
│       │   └── Utilities/
│       │       ├── LSExtensionHelpers.cs
│       │       ├── LSSemaphore.cs
│       │       └── LSSignals.cs
│       ├── Collections/
│       │   ├── BinaryHeap.cs
│       │   ├── CachePool.cs
│       │   └── ICachePool.cs
│       ├── Random/
│       │   └── LehmerRandom.cs (renomeado)
│       ├── Exceptions/
│       │   ├── LSArgumentException.cs
│       │   ├── LSArgumentNullException.cs
│       │   ├── LSExceptions.cs
│       │   ├── LSNotImplementedException.cs
│       │   └── LSNullReferenceException.cs
│       ├── Graphs/
│       │   ├── Core/
│       │   │   ├── Interfaces.cs
│       │   │   ├── GridNeighbour.cs
│       │   │   └── Exceptions.cs
│       │   ├── Implementations/
│       │   │   ├── GridGraph.cs
│       │   │   ├── HexGraph.cs
│       │   │   └── NodeGraph.cs
│       │   └── PathResolvers/
│       │       ├── AStarPathResolver.cs
│       │       └── DijkstraPathResolver.cs
│       ├── Hex/
│       │   └── Hex.cs
│       ├── Serialization/
│       │   └── JsonConverters/
│       │       ├── InvariantCultureDoubleConverter.cs
│       │       ├── InvariantCultureFloatConverter.cs
│       │       ├── InvariantCultureIntConverter.cs
│       │       ├── InvariantCultureLongConverter.cs
│       │       ├── LSSerializerInfoConverter.cs
│       │       ├── LSSerializerInfoListConverter.cs
│       │       └── SystemGuidConverter.cs
│       ├── Localization/
│       │   ├── FormatterToken.cs
│       │   └── Languages.cs
│       ├── Logging/
│       │   ├── Core/
│       │   │   ├── ILSLogProvider.cs
│       │   │   ├── LSLogEntry.cs
│       │   │   └── LSLogger.cs
│       │   ├── Providers/
│       │   │   └── LSLogProviders.cs
│       │   └── docs/
│       │       ├── README.md
│       │       └── QUICK_REFERENCE.md
│       └── ProcessSystem/
│           ├── Core/
│           │   ├── Interfaces/
│           │   │   ├── ILSProcessable.cs
│           │   │   ├── ILSProcessLayerNode.cs
│           │   │   └── ILSProcessNode.cs
│           │   ├── LSProcess.cs
│           │   ├── LSProcessManager.cs
│           │   ├── LSProcessSession.cs
│           │   └── LSProcessSessionGeneric.cs
│           ├── Nodes/
│           │   ├── LSProcessNodeHandler.cs
│           │   ├── LSProcessNodeCondition.cs
│           │   ├── LSProcessNodeSequence.cs
│           │   ├── LSProcessNodeSelector.cs
│           │   ├── LSProcessNodeParallel.cs
│           │   └── LSProcessNodeInverter.cs
│           ├── Builder/
│           │   ├── LSProcessTreeBuilder.cs
│           │   ├── LSProcessBuilderAction.cs
│           │   └── LSProcessHelpers.cs
│           ├── Types/
│           │   ├── LSProcessPriority.cs
│           │   ├── LSProcessResultStatus.cs
│           │   ├── LSProcessLabels.cs
│           │   └── LSProcessLayerNodeType.cs
│           └── docs/
│               └── QUICK_GUIDE.md
├── tests/
│   ├── LSUtils.Tests/
│   │   ├── Core/
│   │   │   ├── LSVersionTests.cs
│   │   │   ├── LSTimestampTests.cs
│   │   │   └── LSMathTests.cs
│   │   ├── Collections/
│   │   │   ├── BinaryHeapTests.cs
│   │   │   └── CachePoolTests.cs
│   │   ├── Random/
│   │   │   └── LehmerRandomTests.cs
│   │   ├── Graphs/
│   │   │   ├── GridGraphTests.cs
│   │   │   ├── HexGraphTests.cs
│   │   │   ├── AStarTests.cs
│   │   │   └── DijkstraTests.cs
│   │   ├── Logging/
│   │   │   └── LSLoggerTests.cs
│   │   └── ProcessSystem/
│   │       ├── LSProcess_Tests.cs
│   │       ├── LSProcessManager_Tests.cs
│   │       ├── Nodes/
│   │       │   ├── LSProcessNodeSequence_Tests.cs
│   │       │   ├── LSProcessNodeSelector_Tests.cs
│   │       │   ├── LSProcessNodeInverter_Tests.cs
│   │       │   └── LSProcessNodeParallel_Tests.cs
│   │       ├── Integration/
│   │       │   ├── ComplexIntegrationTests.cs
│   │       │   ├── ErrorHandlingTests.cs
│   │       │   ├── HandlerNodeTests.cs
│   │       │   └── MergeOperationsTests.cs
│   │       └── TreeBuilder_Tests.cs
│   └── LSUtils.Benchmarks/ (novo)
│       ├── ProcessSystemBenchmarks.cs
│       ├── CollectionsBenchmarks.cs
│       └── GraphsBenchmarks.cs
├── .gitignore
├── CHANGELOG.md (novo)
├── LICENSE (verificar se existe)
├── LSUtils.sln (novo)
├── LSUtils.csproj → src/LSUtils/LSUtils.csproj (mover)
├── MIGRATION_PLAN.md (remover)
├── README.md (novo)
```

### Namespaces Propostos

```csharp
// Core
LSUtils.Core
LSUtils.Core.Interfaces
LSUtils.Core.Types
LSUtils.Core.Math
LSUtils.Core.Delegates
LSUtils.Core.Utilities

// Subsistemas
LSUtils.Collections
LSUtils.Random
LSUtils.Exceptions
LSUtils.Graphs
LSUtils.Graphs.PathResolvers
LSUtils.Hex
LSUtils.Serialization.JsonConverters
LSUtils.Localization
LSUtils.Logging
LSUtils.ProcessSystem
LSUtils.ProcessSystem.Nodes
LSUtils.ProcessSystem.Builder
```

---

## 📝 Plano de Ação

### Fase 1: Preparação (2-3 horas)

**Prioridade:** 🔴 Alta

#### 1.1 Configuração de Build

- [ ] Ajustar `.csproj`:
  - Remover `IsTestProject=true`
  - Habilitar `GenerateAssemblyInfo=true`
  - Adicionar metadados (versão, autor, descrição)
  - Configurar package para NuGet

#### 1.2 Documentação Base

- [ ] Criar `README.md` principal
- [ ] Criar `CHANGELOG.md`
- [ ] Criar estrutura de `docs/`

---

### Fase 2: Reorganização de Código (4-6 horas)

**Prioridade:** 🔴 Alta

#### 2.1 Mover Testes

- [ ] Criar projeto `tests/LSUtils.Tests/`
- [ ] Mover todos os testes de `src/Tests/`
- [ ] Ajustar namespaces para `LSUtils.Tests.*`
- [ ] Verificar que todos os testes passam

#### 2.2 Reorganizar Core

- [ ] Criar subpastas: `Core/Interfaces/`, `Core/Types/`, `Core/Math/`, `Core/Delegates/`, `Core/Utilities/`
- [ ] Mover arquivos apropriados
- [ ] Ajustar namespaces
- [ ] Atualizar referências

#### 2.3 Reorganizar Subsistemas

- [ ] **Random:**
  - Renomear `Random.cs` → `LehmerRandom.cs`
  - Mover para `Random/`
  - Namespace: `LSUtils.Random`
  
- [ ] **Graphs:**
  - Criar `Core/`, `Implementations/`, `PathResolvers/`
  - Mover arquivos apropriados
  - Ajustar namespaces
  
- [ ] **Serialization:**
  - Renomear `JsonConverters/` → `Serialization/JsonConverters/`
  - Ajustar namespace
  
- [ ] **Localization:**
  - Renomear `Locale/` → `Localization/`
  - Ajustar namespace

#### 2.4 Reorganizar ProcessSystem e Logging

- [ ] Manter estrutura com docs integrados
- [ ] Adicionar subpastas conforme proposto
- [ ] Ajustar namespaces

---

### Fase 3: Testes e Cobertura (4-6 horas)

**Prioridade:** 🔴 Alta
**Status:** ⚠️ Bloqueada - Requer atualização de API

#### ⚠️ Problema Crítico Identificado

Os testes existentes do ProcessSystem foram escritos para uma API antiga e não compilam mais.

**Mudança de API:**

```csharp
// ❌ API ANTIGA (usada nos testes atuais)
var builder = new LSProcessTreeBuilder();  // Construtor removido
builder.Handler(...);
var tree = builder.Build();
var session = new LSProcessSession(null!, process, tree);  // Construtor agora é internal
var result = session.Execute();

// ✅ API MODERNA (implementação atual)
var process = new MockProcess();
process.WithProcessing(builder => builder
    .Handler("nodeID", session => LSProcessResultStatus.SUCCESS)
);
var result = process.Execute(instances);
```

**Impacto:**
- 297 erros de compilação no projeto de testes
- Todos os 11 arquivos de teste afetados
- 100% dos testes do ProcessSystem precisam ser reescritos

**Ver:** `tests/LSUtils.Tests/README.md` para detalhes completos

#### 3.1 Atualizar Testes do ProcessSystem

- [ ] **Migrar para API moderna:**
  - `TreeBuilder_Tests.cs` - Testar através de `LSProcess.Execute()`
  - `LSProcessingSystem_ComplexIntegrationTests.cs`
  - `LSProcessingSystem_ErrorHandlingTests.cs`
  - `LSProcessingSystem_HandlerNodeTests.cs`
  - `LSProcessingSystem_MergeOperationsTests.cs`
  - `LSProcessingSystem_ParallelNodeTests.cs`
  - `LSProcessNodeCondition_Tests.cs`
  - `LSProcessNodeInverter_Tests.cs`
  - `LSProcessNodeSelector_Tests.cs`
  - `LSProcessNodeSequence_Tests.cs`

- [ ] Verificar que todos os testes passam

#### 3.2 Criar Novos Testes

- [ ] **Core:** LSVersion, LSTimestamp, LSMath, LSExtensionHelpers
- [ ] **Collections:** BinaryHeap, CachePool
- [ ] **Random:** LehmerRandom
- [ ] **Graphs:** GridGraph, HexGraph, NodeGraph, PathResolvers
- [ ] **Logging:** LSLogger completo
- [ ] **Hex:** Sistema de coordenadas hexagonais

#### 3.3 Configurar Cobertura

- [ ] Adicionar pacote Coverlet ao projeto de testes
- [ ] Configurar geração de relatórios HTML
- [ ] Meta: 80% cobertura mínima
- [ ] Integrar com CI/CD

---

### Fase 4: Documentação Completa (4-6 horas)

**Prioridade:** 🟡 Média

#### 4.1 README Principal

- [ ] Visão geral do projeto
- [ ] Instalação e quick start
- [ ] Exemplos básicos
- [ ] Links para documentação detalhada
- [ ] Badges (build, cobertura, versão)

#### 4.2 Guias de Usuário

- [ ] `docs/getting-started.md`
- [ ] `docs/guides/process-system-guide.md`
- [ ] `docs/guides/logging-guide.md`
- [ ] `docs/guides/graph-guide.md`

#### 4.3 Referência de API

- [ ] Documentar interfaces públicas
- [ ] Documentar classes principais
- [ ] Adicionar XML comments

#### 4.4 Exemplos

- [ ] Criar projetos de exemplo em `samples/`
- [ ] Documentar casos de uso comuns

---

## 📚 Documentação Detalhada

### README.md Principal - Estrutura Proposta

```markdown
# LSUtils

[![Build Status](badge)]()
[![Test Coverage](badge)]()
[![NuGet Version](badge)]()
[![License](badge)]()

Uma biblioteca .NET utilitária modular com componentes para processamento, logging, grafos e mais.

## ✨ Características

- 🔄 **Process System**: Sistema flexível de processos e behaviour trees
- 📝 **Logging**: Sistema de logging multi-provider com suporte a contexto
- 🗺️ **Graphs**: Implementações de grafos com A* e Dijkstra
- 📦 **Collections**: Estruturas de dados especializadas (BinaryHeap, CachePool)
- 🎲 **Random**: Gerador Lehmer de números aleatórios
- 🔷 **Hex**: Sistema de coordenadas hexagonais
- 🌍 **Localization**: Suporte a localização e formatação

## 📦 Instalação

```bash
dotnet add package LSUtils
```

## 🚀 Quick Start

### Process System

```csharp
var process = LSProcess.Create("example", builder => builder
    .Sequence("main", seq => seq
        .Handler("task1", () => Task1())
        .Handler("task2", () => Task2())
    )
);
```

### Logging

```csharp
var logger = new LSLogger("MyApp");
logger.Info("Application started");
```

[Ver mais exemplos →](docs/getting-started.md)

## 📖 Documentação

- [Getting Started](docs/getting-started.md)
- [Process System Guide](docs/guides/process-system-guide.md)
- [Logging Guide](docs/guides/logging-guide.md)
- [Graph Guide](docs/guides/graph-guide.md)
- [API Reference](docs/api-reference/)

## 🤝 Contribuindo

Contribuições são bem-vindas! Veja [CONTRIBUTING.md](CONTRIBUTING.md).

## 📄 Licença

[LICENSE](LICENSE)

## 🙏 Agradecimentos

...

```

---

## 🧪 Estratégia de Testes

### Pirâmide de Testes

```

        ╱╲
       ╱  ╲
      ╱ E2E ╲         10% - Integration Tests
     ╱‾‾‾‾‾‾‾‾╲
    ╱          ╲
   ╱ Integration╲     20% - Integration Tests
  ╱‾‾‾‾‾‾‾‾‾‾‾‾‾‾╲
 ╱                ╲
╱   Unit Tests     ╲   70% - Unit Tests
‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾

```

### Cobertura por Componente

| Componente | Cobertura Atual | Meta | Prioridade |
|------------|----------------|------|-----------|
| ProcessSystem | ~80% | 90% | 🔴 Alta |
| Logging | 0% | 80% | 🟡 Média |
| Graphs | 0% | 80% | 🟡 Média |
| Collections | 0% | 85% | 🟡 Média |
| Core | 0% | 75% | 🟢 Baixa |
| Random | 0% | 70% | 🟢 Baixa |
| Hex | 0% | 70% | 🟢 Baixa |

### Ferramentas
- **Framework:** NUnit
- **Cobertura:** Coverlet
- **Mocks:** NSubstitute (adicionar se necessário)
- **Benchmarks:** BenchmarkDotNet (adicionar)

---

## 🔧 CI/CD

### Workflows

#### Build Workflow
```yaml
name: Build
on: [push, pull_request]
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: 8.0.x
      - name: Restore
        run: dotnet restore
      - name: Build
        run: dotnet build --no-restore
```

#### Test Workflow

```yaml
name: Test
on: [push, pull_request]
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
      - name: Test
        run: dotnet test --collect:"XPlat Code Coverage"
      - name: Upload coverage
        uses: codecov/codecov-action@v3
```

---

## 📋 Checklist de Conclusão

### Estrutura

- [ ] Código reorganizado em estrutura modular
- [ ] Namespaces consistentes
- [ ] Testes separados do código de produção
- [ ] `.editorconfig` configurado
- [ ] `.gitignore` atualizado

### Documentação

- [ ] README.md principal completo
- [ ] Todos os guias criados
- [ ] Exemplos funcionando
- [ ] CHANGELOG.md atualizado
- [ ] CONTRIBUTING.md criado

### Testes

- [ ] Cobertura mínima de 80% alcançada
- [ ] Todos os testes passando
- [ ] Testes organizados por componente
- [ ] Relatórios de cobertura funcionando

---

## 📅 Cronograma Estimado

| Fase | Duração | Início | Conclusão |
|------|---------|--------|-----------|
| Fase 1: Preparação | 2-3h | - | - |
| Fase 2: Reorganização | 4-6h | - | - |
| Fase 3: Testes | 3-4h | - | - |
| Fase 4: Documentação | 4-6h | - | - |
| **Total** | **16-24h** | - | - |

---

## 🎯 Próximos Passos Imediatos

1. ✅ Criar este documento de planejamento
2. ⏭️ Começar Fase 1: Preparação
3. ⏭️ Criar README.md principal
4. ⏭️ Configurar estrutura básica de build

---

**Última Atualização:** 29/12/2025
