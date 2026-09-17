# CLAUDE CODE — DESKTOPTASKS V0.7.1
## Refatoração do módulo de Provas e do Planejador de Estudos
### Árvore visual + grafo de dependências + limites humanos de estudo

> **Base obrigatória:** partir do DesktopTasks atual e funcional.  
> Não reescrever o aplicativo do zero.  
> Preservar os recursos existentes e migrar os dados atuais com segurança.  
> Esta atualização corrige o modelo de domínio e o planejador de estudos; não é apenas um ajuste visual.

---

# 1. Problemas observados no uso real

Existem duas falhas conceituais principais.

## 1.1 Dependências entre conteúdos não são respeitadas

Exemplo:

```text
Lista 3 depende da Lista 2.
```

Se a Lista 2 ainda não foi concluída, o planejador não pode sugerir uma sessão da Lista 3.

A Lista 3 não deve receber apenas prioridade menor. Ela deve ser considerada **bloqueada**.

Se houver 1h disponível e existir outro conteúdo elegível, por exemplo:

```text
Flexão Bi-Axial — teoria — 1h
```

o planejador deve usar esse tempo em um item executável e manter a Lista 3 bloqueada.

## 1.2 Capacidade geral do dia está sendo confundida com capacidade de uma matéria

Se o usuário disser:

```text
Capacidade geral do dia = 8h
```

isso significa que existem aproximadamente 8h disponíveis para diferentes atividades.

Não significa que ele consegue estudar 8h da mesma prova.

O planejador precisa separar:

1. capacidade geral diária;
2. meta confortável diária da prova/matéria;
3. máximo diário permitido para a prova/matéria;
4. máximo de duração de uma sessão contínua.

---

# 2. Novo modelo mental do módulo de Provas

A prova deve ser visualmente representada como uma **árvore**.

Internamente, as dependências entre elementos devem formar um **grafo direcionado acíclico (DAG)**.

Resumo:

```text
Interface = árvore
Dependências = grafo direcionado
```

Isso permite que um item de um ramo dependa de um item de outro ramo.

---

# 3. Exemplo da nova árvore

```text
PROVA — MPP-31
Data: 10/09

▼ Torção
   ├─ Teoria                         1h
   └─ Lista 1                        1h

▼ Flexão Bi-Axial
   ├─ Teoria                         1h
   └─ Lista 2                        45 min

▼ Seções de Paredes Finas
   ├─ Teoria                         2h
   └─ Lista 3                        3h
```

A estrutura deve ser montável em uma mesma tela, sem exigir uma sequência longa de diálogos separados.

---

# 4. Fluxo de criação da prova

Ao clicar em:

```text
+ Nova prova
```

usar um fluxo único ou wizard simples.

## Etapa 1 — Dados gerais

```text
Nome/Disciplina
[ MPP-31 ]

Data da prova
[ 10/09/2026 ]

Meta confortável de estudo por dia
[ 2h ]

Máximo desta matéria/prova por dia
[ 3h ]

Máximo de uma sessão contínua
[ 1h30 ]
```

Esses três limites de estudo têm significados diferentes e devem ser armazenados separadamente.

---

# 5. Limites de estudo

## 5.1 Meta confortável diária

Exemplo:

```text
2h/dia
```

É uma **soft constraint**.

O planejador deve tentar ficar próximo desse valor em dias normais, mas pode ultrapassá-lo quando necessário, desde que nunca ultrapasse o máximo diário.

## 5.2 Máximo diário da prova/matéria

Exemplo:

```text
3h/dia
```

É uma **hard constraint**.

O planejador automático nunca pode ultrapassar esse valor sozinho.

Mesmo se:

```text
Capacidade geral do dia = 24h
```

MPP-31 continua limitada ao seu máximo diário.

## 5.3 Máximo por sessão contínua

Exemplo:

```text
1h30
```

É uma **hard constraint por sessão**.

Se houver 3h de MPP-31 em um dia e o item puder ser dividido, não gerar:

```text
MPP-31 — 3h contínuas
```

Gerar sessões de no máximo 1h30.

Não é necessário criar horários reais nesta atualização; basta dividir em blocos lógicos.

---

# 6. Capacidade geral diária continua existindo

Exemplo:

```text
Sábado
Capacidade geral: 8h

MPP-31
Meta: 2h
Máximo diário: 3h
Máximo por sessão: 1h30
```

Plano válido:

```text
MPP-31                    3h
Relatório                 2h
Outra matéria             2h
Academia                   1h
-----------------------------
Total                      8h
```

Plano inválido:

```text
MPP-31                    8h
```

O planejador não precisa preencher 100% da capacidade do dia.

---

# 7. Nova entidade genérica: StudyNode

Refatorar o modelo específico de conteúdos/listas para uma entidade genérica de estudo.

Sugestão:

```text
Exam
└── StudyNode
```

Tipos possíveis:

```text
Topic
Theory
ExerciseList
Review
Custom
```

Campos sugeridos:

```text
Id
ExamId
ParentNodeId nullable
NodeType
Title
Description nullable
EstimatedMinutes nullable
CanSplit
MasteryLevel nullable
SortOrder
IsCompleted
CompletedAt nullable
CreatedAt
```

`ParentNodeId` representa a árvore visual.

---

# 8. Dependências: StudyDependency

Criar entidade/tabela:

```text
StudyDependency
```

Campos sugeridos:

```text
Id
ExamId
NodeId
DependsOnNodeId
```

Significado:

```text
NodeId só pode ser executado quando DependsOnNodeId estiver concluído.
```

---

# 9. Regra de desbloqueio

Uma dependência só é satisfeita quando o item anterior estiver realmente concluído:

```text
IsCompleted = true
```

NÃO considerar suficiente:

- PlannedDate ter passado;
- item estar planejado;
- sessão ter sido criada;
- sessão ter sido parcialmente executada;
- item estar marcado para ontem.

Somente conclusão real desbloqueia o dependente.

---

# 10. Dependências múltiplas

Um StudyNode pode depender de vários outros.

Exemplo:

```text
Lista 3 depende de:
✓ Lista 2
✓ Teoria de Paredes Finas
```

Regra:

```text
eligible(node) = todas as dependências concluídas
```

O nó só fica elegível quando **todas** as dependências estiverem concluídas.

---

# 11. Prevenção de ciclos

O grafo de dependências deve permanecer um DAG.

Impedir:

```text
Lista 1 -> Lista 2
Lista 2 -> Lista 3
Lista 3 -> Lista 1
```

Antes de salvar uma dependência:

- impedir self-dependency;
- impedir duplicidade;
- verificar se cria ciclo;
- se criar, rejeitar e informar o usuário.

---

# 12. Dependência automática teoria -> lista

Ao criar uma lista dentro de um assunto com teoria, oferecer por padrão:

```text
☑ Esta lista depende da teoria deste assunto
```

Deixar marcado por padrão.

O usuário deve poder desmarcar.

Exemplo:

```text
▼ Flexão Bi-Axial
   ├─ Teoria
   └─ Lista 2
       ☑ depende de Flexão Bi-Axial — Teoria
```

---

# 13. Dependências cruzadas

Permitir selecionar itens de outros ramos.

Exemplo:

```text
Lista 3

Depende de:
[ Lista 2                     ]
[ Paredes Finas — Teoria     ]
```

Usar seletor simples, pesquisável ou checklist.

---

# 14. Editor de árvore

Evitar o fluxo de cadastrar cada conteúdo isoladamente.

Preferir:

```text
MPP-31

+ Adicionar assunto

▼ Torção
   + Teoria
   + Lista
   + Outro

▼ Flexão Bi-Axial
   + Teoria
   + Lista
   + Outro

▼ Paredes Finas
   + Teoria
   + Lista
   + Outro
```

Objetivo: reduzir muito o número de cliques.

Preferir edição inline e/ou painel lateral em vez de janela separada para cada nó.

---

# 15. Informações de cada nó

Conforme o tipo, permitir:

```text
Título
Tipo
Estimativa de duração
CanSplit
Nível de domínio
Dependências
```

Para lista, preservar `QuestionCount` se já existir.

---

# 16. Níveis de domínio

Preservar:

```text
Não estudado
Fraco
Razoável
Bom
```

Isso é uma soft preference do planejador.

Um conteúdo `Não estudado` não pode ultrapassar uma dependência dura.

---

# 17. Hard constraints e soft preferences

Refatorar conceitualmente o motor.

## Hard constraints

O planejador NÃO pode violar:

```text
- dependência ainda não concluída;
- capacidade geral diária = 0;
- máximo diário da prova;
- máximo por sessão;
- item não divisível;
- data física da prova/prazo.
```

## Soft preferences

O planejador tenta otimizar:

```text
- meta confortável diária;
- antecipação;
- margem antes da prova;
- prioridade manual;
- conteúdo fraco/não estudado;
- espaçamento;
- variedade de trabalho;
- PlannedDate existente;
- liberar caminhos do grafo.
```

Hard constraint sempre vence soft preference.

---

# 18. Estado bloqueado na interface

Mostrar claramente:

```text
🔒 Lista 3
   Requer:
   - Lista 2
   - Paredes Finas — Teoria
```

Não depender apenas de cor.

Estados sugeridos:

```text
○ pendente
✓ concluído
🔒 bloqueado
◐ parcial
```

---

# 19. Itens bloqueados e o planejador

Um item bloqueado:

- não entra no conjunto de candidatos executáveis;
- não consome capacidade;
- não gera sessão antecipada;
- não aparece como principal sugestão do widget;
- pode aparecer no app como futuro/bloqueado.

Regra conceitual:

```text
candidates = pendingNodes.Where(DependenciesSatisfied)
```

---

# 20. Caso real obrigatório

Configuração:

```text
Lista 3
EstimatedMinutes = 180
DependsOn = Lista 2
Lista 2 = pendente

Flexão Bi-Axial — teoria
EstimatedMinutes = 60
Dependencies = nenhuma

Capacidade restante = 60 min
```

Resultado obrigatório:

```text
Flexão Bi-Axial — teoria = planejada
Lista 3 = bloqueada
```

Nunca planejar Lista 3 antes da conclusão da Lista 2.

---

# 21. Sessões de estudo

Sessões devem ser geradas a partir de nós elegíveis.

Modelo sugerido:

```text
StudySession
Id
ExamId
StudyNodeId
PlannedDate
PlannedMinutes
IsCompleted
CompletedAt
```

---

# 22. Divisão de itens longos

Exemplo:

```text
Lista 3
EstimatedMinutes = 180
CanSplit = true
MaxSessionMinutes = 90
```

Pode gerar:

```text
Lista 3 (1/2) — 90 min
Lista 3 (2/2) — 90 min
```

Somente depois das dependências serem satisfeitas.

Se `CanSplit = false`, não dividir.

Se não houver espaço compatível, sinalizar conflito.

---

# 23. Conclusão parcial

Se um nó de 180 min gerar duas sessões de 90 min:

- concluir a primeira não marca o StudyNode inteiro como concluído;
- o nó só fica concluído quando todo o trabalho necessário estiver concluído.

O estado pode ficar `parcial` entre sessões.

---

# 24. Planejamento diário por prova

Para cada dia:

```text
ExamDailyTotal <= MaxDailyStudyMinutes
```

Tentar:

```text
ExamDailyTotal ≈ ComfortableDailyStudyMinutes
```

quando houver tempo suficiente até a prova.

---

# 25. Meta confortável vs máximo

Exemplo:

```text
Meta = 120 min
Máximo = 180 min
```

Com prova distante:

```text
aprox. 120 min/dia
```

Com prova próxima:

```text
150–180 min/dia
```

Nunca ultrapassar 180 min automaticamente.

---

# 26. Máximo por sessão

Se:

```text
MaxSessionMinutes = 90
```

nenhuma sessão automática pode exceder 90 min.

Mesmo que:

```text
MaxDailyStudyMinutes = 240
```

as sessões individuais continuam limitadas a 90 min.

---

# 27. Teste obrigatório das 24 horas

Configurar:

```text
Capacidade geral do dia = 24h

MPP-31:
Meta confortável = 2h
Máximo diário = 3h
Máximo por sessão = 1h30
```

Resultado obrigatório:

- no máximo 3h de MPP-31 no dia;
- nenhuma sessão individual maior que 1h30;
- o restante das 24h não deve ser preenchido à força com MPP-31.

---

# 28. Planejamento impossível

Exemplo:

```text
Prova em 2 dias
Trabalho restante = 10h
Máximo diário = 3h
Capacidade máxima possível = 6h
Déficit = 4h
```

Mostrar:

```text
⚠ Não existe um plano viável com as restrições atuais.

Trabalho restante: 10h
Capacidade possível até a prova: 6h
Déficit: 4h
```

Oferecer ações:

```text
[ Aumentar limite temporariamente ]
[ Ajustar conteúdos ]
[ Ajustar estimativas ]
[ Manter plano incompleto ]
```

Não ultrapassar o máximo silenciosamente.

---

# 29. Margem antes da prova

Preservar a regra:

> quando possível, concluir conteúdo principal antes da véspera.

Como provas frequentemente acontecem de manhã:

- preferir conteúdo principal concluído até D-2;
- usar D-1 como revisão/margem;
- usar D-1 para conteúdo novo apenas se necessário.

Exemplo:

```text
06/09 — conteúdo
07/09 — conteúdo
08/09 — conteúdo
09/09 — revisão/margem
10/09 — PROVA
```

---

# 30. Estratégia de distribuição

Distribuir ao longo do período com tendência moderada a antecipar.

Não concentrar tudo no primeiro dia e não deixar tudo para a véspera.

Considerar:

- dias disponíveis;
- dependências;
- meta confortável;
- máximo diário;
- máximo de sessão;
- duração;
- domínio;
- margem;
- planos manuais existentes.

---

# 31. Variedade de trabalho

Se a meta/máximo da prova for atingida e ainda houver capacidade geral no dia:

não preencher automaticamente com mais da mesma prova.

A capacidade pode ficar disponível para:

- outra prova;
- outra matéria;
- Projeto;
- Entrega;
- tarefa comum;
- rotina;
- ou permanecer livre.

O motor não precisa ocupar 100% do dia.

---

# 32. Liberar caminhos do grafo

Entre itens elegíveis equivalentes, pode haver preferência moderada por nós cuja conclusão desbloqueia outros nós.

Exemplo:

```text
Lista 2 -> Lista 3
```

Concluir Lista 2 libera trabalho futuro.

Isso é uma soft preference, nunca uma hard constraint.

---

# 33. Replanejamento após não conclusão

Se uma sessão não for concluída:

```text
A sessão "MPP-31 — Lista 2" não foi concluída ontem.

Recalcular o plano de estudo?

[ Recalcular ] [ Manter como está ]
```

Nunca recalcular silenciosamente.

---

# 34. Dependências e replanejamento

Se um pré-requisito não foi concluído:

```text
Lista 2 não concluída
↓
Lista 3 continua bloqueada
```

Descendentes bloqueados devem ser retirados das opções executáveis e o sistema deve procurar outros nós elegíveis.

---

# 35. Desbloqueio automático

Quando a última dependência necessária for concluída:

- o nó passa de bloqueado para elegível;
- atualizar a UI;
- não inserir automaticamente no dia atual;
- disponibilizar para o próximo planejamento/replanejamento.

---

# 36. Edição de dependências

Permitir adicionar/remover dependências.

Ao alterar:

- validar ciclos;
- atualizar bloqueios;
- revalidar sessões futuras;
- não apagar histórico silenciosamente.

Se uma sessão futura ficar inválida, marcá-la como necessitando revisão/replanejamento.

---

# 37. Drag-and-drop da árvore

Se tecnicamente razoável:

- permitir reorganizar nós irmãos;
- permitir mover um nó para outro assunto.

Atenção:

> posição na árvore e dependência são conceitos diferentes.

Mover visualmente não deve criar/remover dependências automaticamente.

---

# 38. Configuração padrão ao criar prova

Pode oferecer defaults editáveis:

```text
Meta confortável: 2h
Máximo diário: 3h
Máximo por sessão: 1h30
```

Mas esses valores devem aparecer explicitamente para o usuário; não usar defaults invisíveis.

---

# 39. Validação dos limites

Garantir:

```text
ComfortableDailyStudyMinutes > 0
MaxDailyStudyMinutes > 0
MaxSessionMinutes > 0
ComfortableDailyStudyMinutes <= MaxDailyStudyMinutes
MaxSessionMinutes <= MaxDailyStudyMinutes
```

Se inválido, mostrar erro claro.

---

# 40. Alteração de limites com plano existente

Se o usuário reduzir, por exemplo:

```text
máximo diário: 4h -> 2h
```

mas o plano atual possuir 3h:

```text
O plano atual ultrapassa o novo limite em alguns dias.

[ Recalcular plano ]
[ Manter por enquanto ]
```

Não corrigir silenciosamente.

---

# 41. Migração dos dados atuais

O app já possui provas, conteúdos, listas e sessões.

A migração deve preservar:

1. provas;
2. títulos;
3. datas;
4. estimativas;
5. domínio;
6. listas;
7. conclusões;
8. sessões, quando possível.

Mapeamento sugerido:

```text
ExamTopic -> StudyNode(NodeType = Topic ou Theory)
ExamExerciseList -> StudyNode(NodeType = ExerciseList)
```

Criar relações pai-filho apenas quando puder inferir com segurança.

Não inventar dependências durante migração sem evidência explícita.

Itens ambíguos podem permanecer na raiz para reorganização manual.

---

# 42. Sessões existentes após migração

Revalidar sessões futuras contra:

- dependências;
- máximo diário;
- máximo por sessão.

Não apagar silenciosamente.

Se incompatível:

```text
⚠ Esta sessão precisa ser revisada
```

Oferecer regeneração do plano.

---

# 43. Compatibilidade com o widget

O widget deve continuar simples.

Mostrar apenas sessões/tarefas executáveis.

Não mostrar um item bloqueado como ação principal de Hoje.

Opcionalmente, no app principal pode existir:

```text
2 itens aguardando pré-requisitos
```

Sem poluir o widget.

---

# 44. Arquitetura recomendada

Separar serviços, por exemplo:

```text
StudyGraphService
DependencyValidationService
StudyEligibilityService
ExamPlanningService
StudyCapacityService
StudySessionService
PlanningExplanationService
```

Evitar regras de domínio espalhadas em ViewModels.

---

# 45. StudyEligibilityService

Responsabilidades:

```text
IsEligible(node)
GetBlockingDependencies(node)
```

Regra básica:

```text
IsEligible(node) =
    !node.IsCompleted
    && AllDependenciesCompleted(node)
```

---

# 46. DependencyValidationService

Responsável por:

- self-dependency;
- duplicidade;
- ciclo;
- integridade ao editar/excluir.

Criar testes unitários específicos.

---

# 47. StudyCapacityService

Deve conhecer:

```text
GeneralDailyCapacity
ComfortableDailyStudyMinutes
MaxDailyStudyMinutes
MaxSessionMinutes
DateOverrides
RestDays
```

A capacidade válida da prova em um dia deve respeitar simultaneamente:

```text
capacidade geral restante
máximo diário da prova
máximo por sessão
outras restrições duras
```

---

# 48. ExamPlanningService

Entrada conceitual:

```text
Exam
StudyGraph
CurrentDate
ExamDate
DailyCapacity
ExamStudyPolicy
ExistingManualPlans
```

Saída:

```text
PlanningProposal
Sessions
Conflicts
Explanations
```

Não aplicar automaticamente.

---

# 49. Explicações do planejador

Exemplos:

```text
Flexão Bi-Axial — teoria
Selecionada porque está disponível e ajuda a preparar a Lista 2.
```

```text
Lista 3
Bloqueada: requer Lista 2.
```

```text
MPP-31 atingiu o máximo diário de 3h.
Outras sessões foram movidas para outro dia.
```

```text
Sessão limitada a 1h30 pelo máximo contínuo configurado.
```

O usuário deve conseguir entender o motivo das decisões.

---

# 50. Critérios de aceite — árvore

- [ ] nova prova pode ser montada em árvore;
- [ ] assuntos podem conter teoria/lista/outro;
- [ ] edição não exige dezenas de janelas;
- [ ] hierarquia é persistida;
- [ ] reorganização visual funciona adequadamente.

---

# 51. Critérios de aceite — dependências

- [ ] qualquer StudyNode pode depender de outro;
- [ ] múltiplas dependências funcionam;
- [ ] nó só desbloqueia quando TODAS estão concluídas;
- [ ] PlannedDate passado não desbloqueia;
- [ ] sessão planejada não desbloqueia;
- [ ] ciclos são impedidos;
- [ ] self-dependency é impedida;
- [ ] lista oferece dependência automática da teoria;
- [ ] dependência automática pode ser removida.

---

# 52. Critérios de aceite — planejamento

- [ ] nós bloqueados não geram sessões;
- [ ] nós bloqueados não consomem capacidade;
- [ ] nó elegível pode usar capacidade livre;
- [ ] meta confortável funciona como preferência;
- [ ] máximo diário nunca é ultrapassado automaticamente;
- [ ] máximo por sessão nunca é ultrapassado;
- [ ] tarefas divisíveis podem ser quebradas;
- [ ] tarefas não divisíveis permanecem inteiras;
- [ ] capacidade global e capacidade da prova são distintas;
- [ ] margem antes da prova é preservada quando possível;
- [ ] plano impossível é informado explicitamente.

---

# 53. Testes mínimos obrigatórios

Criar testes para:

1. dependência simples;
2. dependência múltipla;
3. dependência transitiva;
4. detecção de ciclo;
5. self-dependency;
6. desbloqueio após conclusão real;
7. PlannedDate não desbloqueia;
8. nó bloqueado fora dos candidatos;
9. teoria -> lista automática;
10. remoção da dependência automática;
11. máximo diário;
12. meta confortável;
13. máximo por sessão;
14. capacidade geral maior que máximo da prova;
15. `CanSplit = true`;
16. `CanSplit = false`;
17. plano impossível;
18. margem D-1;
19. replanejamento após não conclusão;
20. migração de prova existente;
21. sessão futura incompatível após migração.

---

# 54. Não implementar nesta atualização

Não implementar:

- horários reais do dia;
- Pomodoro;
- repetição espaçada sofisticada;
- IA generativa;
- machine learning;
- previsão de nota;
- cronograma minuto a minuto;
- editor gráfico complexo com linhas e nós livres;
- gamificação.

A árvore visual + grafo de dependências interno são suficientes.

---

# 55. Resultado esperado

O usuário deve conseguir criar:

```text
MPP-31 — 10/09

Meta/dia: 2h
Máximo/dia: 3h
Máximo/sessão: 1h30

▼ Torção
   ├─ Teoria 1h
   └─ Lista 1 1h
       depende de Teoria

▼ Flexão Bi-Axial
   ├─ Teoria 1h
   └─ Lista 2 45m
       depende de Teoria

▼ Paredes Finas
   ├─ Teoria 2h
   └─ Lista 3 3h
       depende de Teoria
       depende de Lista 2
```

O planejador deve produzir somente sequências executáveis.

Nunca:

```text
Lista 3 antes de Lista 2
```

E nunca:

```text
24h de MPP-31 em um dia apenas porque há 24h de capacidade geral.
```

---

# 56. Definition of Done

A atualização está pronta quando:

1. o projeto compila;
2. dados atuais são preservados;
3. provas existentes migram;
4. árvore visual funciona;
5. dependências funcionam;
6. ciclos são impedidos;
7. bloqueios são respeitados;
8. limite diário da prova funciona;
9. máximo por sessão funciona;
10. capacidade geral não é confundida com capacidade da matéria;
11. planejamento impossível é detectado;
12. plano é mostrado antes da aplicação;
13. replanejamento nunca é silencioso;
14. testes obrigatórios passam;
15. README é atualizado;
16. o caso específico `Lista 2 -> Lista 3` está corrigido;
17. o teste de capacidade geral de 24h respeita o máximo de estudo da prova.

---

# 57. Instruções finais ao Claude Code

Antes de alterar código:

1. inspecione a implementação atual do módulo de Provas;
2. identifique modelos e tabelas existentes;
3. identifique como conteúdos/listas atuais se relacionam;
4. identifique como sessões são geradas;
5. identifique onde o planejador usa capacidade diária;
6. desenhe a migração antes de executá-la;
7. crie testes para os bugs atuais antes ou junto da correção.

Implemente em etapas pequenas e testáveis.

Não trate esta solicitação como um patch visual.

Trata-se de uma **refatoração do modelo de domínio e do motor de planejamento de estudos**.

A prioridade é preservar dados, corrigir comportamento e tornar o planejador confiável e explicável.
