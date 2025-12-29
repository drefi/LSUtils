# Plano de Migração: BehaviourTree → LSProcessSystem

## 📊 Análise Comparativa Detalhada

### 1. Comparação de Funcionalidades

| Funcionalidade | BehaviourTree | LSProcessSystem | Status |
|----------------|---------------|-----------------|--------|
| **Sequence (AND)** | ✅ SequenceNode | ✅ LSProcessNodeSequence | ✅ Equivalente |
| **Selector (OR)** | ✅ SelectorNode | ✅ LSProcessNodeSelector | ✅ Equivalente |
| **Parallel** | ✅ ParallelNode | ✅ LSProcessNodeParallel | ⚠️ API diferente |
| **Inverter** | ✅ InverterNode | ✅ LSProcessNodeInverter | ✅ Já implementado |
| **Action/Handler** | ✅ ActionNode | ✅ LSProcessNodeHandler | ✅ Equivalente |
| **Condition** | ✅ Condition() | ✅ LSProcessNodeCondition | ✅ Superior |
| **Splice/Merge** | ✅ Splice() | ✅ Merge() | ✅ Equivalente |
| **StateMachine** | ⚠️ Implementação vazia | ❌ Não necessário | ⚠️ Remover |
| **Context System** | ❌ Não existe | ✅ Multi-nível | ✅ Vantagem |
| **Async Support** | ❌ Não existe | ✅ WAITING/Resume | ✅ Vantagem |
| **Generic Types** | ❌ Não existe | ✅ Type-safe | ✅ Vantagem |
| **Data Storage** | ❌ Não existe | ✅ Process.Data | ✅ Vantagem |

---

## 🔍 Análise de Diferenças Críticas

### A. Parallel Node - API Divergente

#### BehaviourTree

```csharp
// Dois parâmetros: numRequiredToFail E numRequiredToSucceed
builder.Parallel("concurrent", 
    numRequiredToFail: 1,      // Falha se 1 filho falhar
    numRequiredToSucceed: 2);   // Sucesso se 2 filhos sucederem
```

#### LSProcessSystem

```csharp
// Um parâmetro: apenas numRequiredToSucceed
builder.Parallel("concurrent", par => par
    .Handler("task1", Task1)
    .Handler("task2", Task2),
    numRequiredToSucceed: 2);   // Sucesso se 2 filhos sucederem
```

**Ação Necessária:** ✅ LSProcessSystem precisa adicionar `numRequiredToFail` para compatibilidade completa.

---

### B. Inverter Node - Status

**Análise:**

- ✅ `LSProcessNodeInverter` já existe e está implementado
- ✅ Lógica de inversão correta (SUCCESS ↔ FAILURE)
- ⚠️ Falta adicionar ao enum `LSProcessLayerNodeType`
- ⚠️ Falta adicionar método genérico `Inverter<TProcess>()`

**Ação Necessária:** Completar integração (30 minutos)

---

### C. StateMachine Support - Análise Profunda

**Arquivos Envolvidos:**

1. `BehaviourTreeBuilder.cs`:
   - `StateMachine<T>()` - 3 overloads
   - `Transition<T>()`
   - `InState<T>()`

2. `StateMachineNode.cs`:
   - `IFSMBehaviourTreeNode` interface
   - `IBTStateNode` interface
   - `TransitionNode` class
   - `StateMachineNode` class (implementação vazia)

**Problemas Identificados:**

- ❌ `StateMachineNode.Update()` retorna SUCCESS sem lógica
- ❌ `TransitionNode.Update()` retorna SUCCESS sem transição real
- ❌ Não há gerenciamento de estados real
- ❌ API confusa (precisa passar `smNode` explicitamente)

**Decisão:** ❌ **REMOVER COMPLETAMENTE** - Não é FSM real, apenas açúcar sintático sobre condições.

---

## 📁 Inventário de Arquivos

### Arquivos a REMOVER (BehaviourTree)

```
src/Fluent-Behaviour-Tree-master/
├── src/
│   ├── BehaviourTreeBuilder.cs          ❌ REMOVER
│   ├── BehaviourTreeStatus.cs           ❌ REMOVER
│   ├── IBehaviourTreeNode.cs            ❌ REMOVER
│   ├── IParentBehaviourTreeNode.cs      ❌ REMOVER
│   ├── TimeData.cs                      ❌ REMOVER
│   └── Nodes/
│       ├── ActionNode.cs                ❌ REMOVER
│       ├── InverterNode.cs              ❌ REMOVER (já existe no ProcessSystem)
│       ├── ParallelNode.cs              ❌ REMOVER
│       ├── SelectorNode.cs              ❌ REMOVER
│       ├── SequenceNode.cs              ❌ REMOVER
│       └── StateMachineNode.cs          ❌ REMOVER (implementação vazia)
```

**Total:** 11 arquivos para remover

---

### Arquivos a MODIFICAR (ProcessSystem)

```
src/ProcessSystem/
├── LSProcessLayerNodeType.cs           ⚠️ ADICIONAR INVERTER enum
├── LSProcessNodeInverter.cs            ⚠️ CORRIGIR (NodeType, Conditions)
├── LSProcessTreeBuilder.cs             ⚠️ ADICIONAR Inverter<TProcess>()
├── LSProcessNodeParallel.cs            ⚠️ ADICIONAR numRequiredToFail
└── QUICK_GUIDE.md                      ⚠️ ATUALIZAR documentação
```

**Total:** 5 arquivos para modificar

---

## 🎯 Plano de Execução (3 Fases)

### FASE 1: Completar LSProcessSystem (Estimativa: 2-3 horas)

#### Tarefa 1.1: Adicionar INVERTER ao Enum

**Arquivo:** `LSProcessLayerNodeType.cs`

```csharp
public enum LSProcessLayerNodeType { 
    SEQUENCE,
    SELECTOR,
    PARALLEL,
    INVERTER  // ← ADICIONAR
}
```

**Tempo:** 2 minutos

---

#### Tarefa 1.2: Corrigir LSProcessNodeInverter

**Arquivo:** `LSProcessNodeInverter.cs`

**Mudanças:**

1. ✅ Adicionar propriedade `NodeType`
2. ✅ Tornar `ReadOnly` imutável
3. ✅ Adicionar verificação de `Conditions` em `Execute()`
4. ✅ Melhorar logging (childResult + invertedResult)
5. ✅ Adicionar validação completa em `AddChild()`

**Tempo:** 30 minutos

---

#### Tarefa 1.3: Adicionar Método Genérico Inverter

**Arquivo:** `LSProcessTreeBuilder.cs`

```csharp
public LSProcessTreeBuilder Inverter<TProcess>(
    string nodeID,
    System.Action<LSProcessTreeBuilder> builderAction,
    LSProcessPriority? priority = LSProcessPriority.NORMAL,
    bool overrideConditions = false,
    bool readOnly = false,
    params LSProcessNodeCondition<TProcess>?[] conditions) 
    where TProcess : LSProcess {
    
    // Convert generic conditions to non-generic
    var convertedConditions = conditions
        .Where(c => c != null)
        .Select(c => c!.ToCondition())
        .ToArray();
    
    return Inverter(nodeID, builderAction, priority, 
        overrideConditions, readOnly, convertedConditions);
}
```

**Tempo:** 15 minutos

---

#### Tarefa 1.4: Adicionar numRequiredToFail ao Parallel

**Arquivo:** `LSProcessNodeParallel.cs`

**Mudanças:**

1. Adicionar propriedade `NumRequiredToFail`
2. Atualizar lógica de `Execute()` para considerar falhas
3. Atualizar `GetNodeStatus()` com lógica de falha
4. Adicionar parâmetro em `LSProcessTreeBuilder.Parallel()`

**Código:**

```csharp
public class LSProcessNodeParallel : ILSProcessLayerNode {
    // ...existing code...
    
    public int NumRequiredToFail { get; internal set; }  // ← ADICIONAR
    
    public LSProcessResultStatus Execute(LSProcessSession session) {
        // ...existing filtering code...
        
        int numChildrenSucceeded = 0;
        int numChildrenFailed = 0;
        
        foreach (var child in _availableChildren) {
            var result = child.Execute(session);
            
            if (result == LSProcessResultStatus.CANCELLED) {
                return LSProcessResultStatus.CANCELLED;
            }
            
            if (result == LSProcessResultStatus.SUCCESS) {
                numChildrenSucceeded++;
            } else if (result == LSProcessResultStatus.FAILURE) {
                numChildrenFailed++;
            }
            
            // Check thresholds
            if (NumRequiredToSucceed > 0 && 
                numChildrenSucceeded >= NumRequiredToSucceed) {
                return LSProcessResultStatus.SUCCESS;
            }
            
            if (NumRequiredToFail > 0 && 
                numChildrenFailed >= NumRequiredToFail) {
                return LSProcessResultStatus.FAILURE;
            }
        }
        
        // If no threshold met, check WAITING
        // ...existing WAITING logic...
    }
}
```

**Atualizar Builder:**

```csharp
public LSProcessTreeBuilder Parallel(
    string nodeID,
    LSProcessBuilderAction builder,
    int numRequiredToSucceed = -1,
    int numRequiredToFail = -1,     // ← ADICIONAR
    LSProcessPriority? priority = null,
    bool overrideConditions = false,
    bool readOnly = false,
    params LSProcessNodeCondition?[] conditions) {
    
    // ...existing logic...
    
    parallelNode.NumRequiredToFail = numRequiredToFail;  // ← ADICIONAR
    
    // ...rest of code...
}
```

**Tempo:** 1 hora

---

#### Tarefa 1.5: Atualizar Documentação

**Arquivo:** `QUICK_GUIDE.md`

**Mudanças:**

1. Documentar `Inverter()` e `Inverter<TProcess>()`
2. Atualizar `Parallel()` com `numRequiredToFail`
3. Adicionar exemplos de uso
4. Notas sobre migração de BehaviourTree

**Tempo:** 30 minutos

---

### FASE 2: Buscar e Migrar Código Dependente (Estimativa: 2-4 horas)

#### Tarefa 2.1: Identificar Dependências

```powershell
# Buscar todos os usos de BehaviourTreeBuilder
Get-ChildItem -Recurse -Include *.cs | 
    Select-String -Pattern "BehaviourTree" | 
    Group-Object Path

# Buscar usos específicos
Get-ChildItem -Recurse -Include *.cs | 
    Select-String -Pattern "(new BehaviourTreeBuilder|IBehaviourTreeNode|TimeData)"
```

**Tempo:** 15 minutos

---

#### Tarefa 2.2: Criar Guia de Migração

**Arquivo:** `BEHAVIOUR_TREE_MIGRATION_GUIDE.md`

**Conteúdo:**

```markdown
# Guia de Migração: BehaviourTree → LSProcessSystem

## Mapeamento de APIs

### Nodes
| BehaviourTree | LSProcessSystem |
|---------------|-----------------|
| `Sequence("name")` | `Sequence("name", seq => ...)` |
| `Selector("name")` | `Selector("name", sel => ...)` |
| `Parallel("name", fail, succeed)` | `Parallel("name", par => ..., succeed, fail)` |
| `Inverter("name")` | `Inverter("name", inv => ...)` |
| `Do("name", fn)` | `Handler("name", handler)` |
| `Condition("name", fn)` | `Handler("name", condition-handler)` |
| `Splice(subTree)` | `Merge(subTree)` |

### Execution
| BehaviourTree | LSProcessSystem |
|---------------|-----------------|
| `tree.Tick(timeData)` | `process.Execute()` |
| `BehaviourTreeStatus.Success` | `LSProcessResultStatus.SUCCESS` |
| `BehaviourTreeStatus.Failure` | `LSProcessResultStatus.FAILURE` |
| `BehaviourTreeStatus.Running` | `LSProcessResultStatus.WAITING` |

## Exemplos de Migração

### Antes (BehaviourTree)
```csharp
var tree = new BehaviourTreeBuilder()
    .Sequence("main")
        .Do("action1", t => {
            DoSomething();
            return BehaviourTreeStatus.Success;
        })
        .Selector("fallback")
            .Do("try1", t => BehaviourTreeStatus.Failure)
            .Do("try2", t => BehaviourTreeStatus.Success)
        .End()
        .Do("action2", t => BehaviourTreeStatus.Success)
    .End()
    .Build();

// Execute
tree.Tick(new TimeData { deltaTime = 0.016f });
```

### Depois (LSProcessSystem)

```csharp
public class MyProcess : LSProcess {
    protected override LSProcessTreeBuilder processing(
        LSProcessTreeBuilder builder) {
        return builder
            .Sequence("main", seq => seq
                .Handler("action1", s => {
                    DoSomething();
                    return LSProcessResultStatus.SUCCESS;
                })
                .Selector("fallback", sel => sel
                    .Handler("try1", s => LSProcessResultStatus.FAILURE)
                    .Handler("try2", s => LSProcessResultStatus.SUCCESS))
                .Handler("action2", s => LSProcessResultStatus.SUCCESS));
    }
}

// Execute
var process = new MyProcess();
var result = process.Execute();
```

## Mudanças Importantes

1. **Delegates em vez de Stack:**
   - BT: `.End()` para fechar contextos
   - PS: Lambdas automáticos (sem `.End()`)

2. **TimeData não existe:**
   - BT: `TimeData` passado a cada tick
   - PS: Use propriedades do Process

3. **StateMachine removido:**
   - Use propriedades do Process + conditions
   - Ver exemplos na documentação

4. **Context System:**
   - PS permite registro global/instância/local
   - BT não tinha este conceito

```

**Tempo:** 1 hora

---

#### Tarefa 2.3: Migrar Código Existente

**Para cada arquivo que usa BehaviourTree:**

1. Criar classe `Process` correspondente
2. Converter builder calls
3. Adaptar TimeData para Process properties
4. Converter Tick() para Execute()
5. Testes de regressão

**Tempo:** 1-3 horas (dependendo do volume)

---

### FASE 3: Remover BehaviourTree (Estimativa: 30 minutos)

#### Tarefa 3.1: Deprecar BehaviourTree

**Antes de remover, marcar como obsoleto:**

```csharp
namespace FluentBehaviourTree {
    /// <summary>
    /// [DEPRECATED] Use LSProcessSystem instead.
    /// This class will be removed in version 2.0.
    /// See BEHAVIOUR_TREE_MIGRATION_GUIDE.md for migration instructions.
    /// </summary>
    [Obsolete("Use LSProcessSystem instead. Will be removed in v2.0", error: false)]
    public class BehaviourTreeBuilder {
        // ...existing code...
    }
}
```

**Tempo:** 5 minutos

---

#### Tarefa 3.2: Atualizar Testes

1. Remover testes de BehaviourTree
2. Adicionar testes de Inverter no ProcessSystem
3. Adicionar testes de numRequiredToFail no Parallel

**Tempo:** 15 minutos

---

#### Tarefa 3.3: Remover Arquivos

```powershell
# Remover pasta BehaviourTree
Remove-Item -Path "src/Fluent-Behaviour-Tree-master" -Recurse -Force
```

**Tempo:** 2 minutos

---

#### Tarefa 3.4: Limpar Referências

1. Atualizar `.csproj` se necessário
2. Limpar `using` statements
3. Rebuild solution

**Tempo:** 5 minutos

---

## 📋 Checklist de Execução

### FASE 1: Completar LSProcessSystem

- [ ] 1.1 Adicionar `INVERTER` ao enum
- [ ] 1.2 Corrigir `LSProcessNodeInverter`
- [ ] 1.3 Adicionar `Inverter<TProcess>()`
- [ ] 1.4 Adicionar `numRequiredToFail` ao Parallel
- [ ] 1.5 Atualizar documentação
- [ ] **Testes:** Validar todas as mudanças

### FASE 2: Migrar Código Dependente

- [ ] 2.1 Identificar dependências
- [ ] 2.2 Criar guia de migração
- [ ] 2.3 Migrar código existente
- [ ] **Testes:** Regressão completa

### FASE 3: Remover BehaviourTree

- [ ] 3.1 Deprecar BehaviourTree (tag obsolete)
- [ ] 3.2 Atualizar/remover testes
- [ ] 3.3 Remover arquivos
- [ ] 3.4 Limpar referências
- [ ] **Build:** Verificar que tudo compila

---

## ⏱️ Estimativa Total de Tempo

| Fase | Tempo Estimado | Complexidade |
|------|----------------|--------------|
| **Fase 1** | 2-3 horas | Média |
| **Fase 2** | 2-4 horas | Alta (depende do volume) |
| **Fase 3** | 30 minutos | Baixa |
| **TOTAL** | **4.5-7.5 horas** | - |

---

## ⚠️ Riscos e Mitigações

### Risco 1: Código dependente não encontrado

**Mitigação:** Usar deprecation warning (FASE 3.1) antes de remover, aguardar feedback.

### Risco 2: Funcionalidade esquecida

**Mitigação:** Análise completa de features (feita neste documento).

### Risco 3: Breaking changes em produção

**Mitigação:** Versionamento semântico (v2.0 para breaking change).

### Risco 4: Testes incompletos

**Mitigação:** Suite completa de testes para ProcessSystem antes de remover BT.

---

## 🎯 Benefícios da Migração

### Redução de Complexidade

- ✅ **-30% de código** (11 arquivos removidos)
- ✅ **API única** e consistente
- ✅ **Zero duplicação** de lógica

### Ganhos Funcionais

- ✅ **Async/await support** (WAITING/Resume)
- ✅ **Multi-level contexts** (global/instance/local)
- ✅ **Type-safe generics** (menos casting)
- ✅ **Data storage** (inter-handler communication)

### Manutenibilidade

- ✅ **Single source of truth** para behavior trees
- ✅ **Documentação unificada**
- ✅ **Menos confusão** para desenvolvedores

---

## 📚 Referências

- [QUICK_GUIDE.md](src/ProcessSystem/QUICK_GUIDE.md) - Documentação do LSProcessSystem
- [LSProcessNodeInverter.cs](src/ProcessSystem/LSProcessNodeInverter.cs) - Implementação do Inverter
- [.github/instructions/lsprocess.instructions.md](.github/instructions/lsprocess.instructions.md) - Instruções de desenvolvimento

---

## ✅ Aprovação e Execução

**Status:** 📋 **PLANO PRONTO PARA EXECUÇÃO**

**Próximo Passo:** Executar FASE 1 (Completar LSProcessSystem)

**Tempo até remoção completa:** 4.5-7.5 horas de trabalho efetivo

---

**Data de criação:** 29/12/2025
**Autor:** GitHub Copilot (Claude Sonnet 4.5)
**Versão:** 1.0
