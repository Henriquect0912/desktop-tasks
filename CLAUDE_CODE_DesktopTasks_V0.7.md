# CLAUDE CODE — DESKTOPTASKS V0.7
## Motor de planejamento, prioridade e capacidade diária

> Base obrigatória: partir da V0.6 funcional. Esta versão introduz o primeiro motor geral de planejamento. O motor deve ser determinístico, explicável, testável e reversível.

## 1. Objetivo

Responder:

> **“Dado tudo o que existe, o que faz mais sentido eu fazer hoje e nos próximos dias?”**

O sistema já conhece tarefas, rotinas, Projetos, Entregas, Provas, subtarefas, sessões, prazos e datas planejadas. Agora deve combinar essas informações com prioridade, duração e capacidade diária.

## 2. Princípio central

O algoritmo:

- sugere;
- explica;
- distribui;
- detecta sobrecarga;

mas o usuário continua no controle.

Nunca replanejar silenciosamente.

## 3. Escopo congelado

Implementar:

1. prioridade manual `Alta / Normal / Baixa`;
2. duração estimada;
3. `CanSplit`;
4. capacidade semanal por dia;
5. dias sem planejamento automático;
6. exceções de capacidade por data;
7. motor de prioridade;
8. distribuição automática;
9. margem antes de prazos e provas;
10. replanejamento assistido;
11. detecção de sobrecarga;
12. explicações de prioridade;
13. plano diário sugerido;
14. revisão diária evoluída;
15. preservação de mudanças manuais;
16. conflitos explícitos;
17. testes abrangentes.

## 4. Fora do escopo

Não implementar IA generativa, machine learning, previsão comportamental, time blocking horário completo, otimização matemática desnecessariamente complexa ou integração externa.

## 5. Prioridade manual

Valores:

```text
Alta
Normal
Baixa
```

É apenas um fator entre vários.

## 6. Duração estimada

Usar minutos:

```text
Relatório — 120 min
Lista 5 — 90 min
Academia — 80 min
```

Quando a duração for desconhecida, preferir estado `UnknownDuration` a inventar precisão.

## 7. CanSplit

Cada item planejável pode ter:

```text
CanSplit = true/false
```

Exemplo divisível: escrever relatório.

Exemplo não divisível: levar carro à oficina.

## 8. Capacidade semanal

Permitir configurar:

```text
SEG  3h
TER  2h
QUA  4h
QUI  2h
SEX  3h
SÁB  6h
DOM  0h
```

`0h` significa: não planejar automaticamente nesse dia.

O usuário ainda pode colocar tarefa manualmente.

## 9. Exceção por data

Permitir sobrescrever a capacidade de uma data específica:

```text
12/09 — 2h
```

A exceção vale apenas para aquela data.

## 10. Fatores do motor

Considerar pelo menos:

1. prazo;
2. proximidade do prazo;
3. atraso no planejamento;
4. prioridade manual;
5. data de prova;
6. domínio fraco/não estudado;
7. duração;
8. capacidade disponível;
9. margem de segurança;
10. tipo do item;
11. `CanSplit`;
12. planejamento manual já existente.

## 11. Explicabilidade

Cada item sugerido deve explicar por que está alto na lista.

Exemplos:

```text
Relatório
Prazo amanhã · prioridade alta
```

```text
MPP-31 — Lista 3
Prova em 3 dias · domínio fraco
```

Não mostrar apenas score numérico.

## 12. Estratégia temporal

Regra:

> distribuir ao longo do período, com tendência moderada a antecipar e preservar folga.

Evitar tanto concentração imediata quanto procrastinação algorítmica até a véspera.

## 13. Margem de segurança

Sempre que possível:

- concluir tarefas antes do `DueDate`;
- concluir preparação principal de prova até pelo menos 1 dia antes;
- usar a véspera como margem/revisão.

## 14. Dias sem capacidade

Se `capacity = 0`, não agendar automaticamente nesse dia.

## 15. Item maior que a capacidade

Se `CanSplit = true`, dividir em sessões.

Se `CanSplit = false`, procurar outro dia com capacidade suficiente.

Se nenhum existir, informar conflito.

## 16. Replanejamento

Se algo não foi concluído:

```text
A tarefa "X" não foi concluída ontem.

Recalcular planejamento?

[ Recalcular ] [ Manter como está ]
```

Nunca recalcular silenciosamente.

## 17. Sobrecarga

Comparar carga e capacidade:

```text
Amanhã
Carga planejada: 7h30
Capacidade: 3h

⚠ Dia sobrecarregado

[ Redistribuir ]
```

## 18. Redistribuição

Ao redistribuir:

1. preservar decisões manuais quando possível;
2. priorizar prazos próximos;
3. respeitar dias de descanso;
4. respeitar capacidade;
5. respeitar `CanSplit`;
6. preservar margem;
7. mostrar proposta antes de aplicar.

## 19. Plano sugerido do dia

Exemplo:

```text
HOJE — PLANO SUGERIDO

1. Fazer relatório — 1h30
2. MPP-31 — Lista 2 — 1h
3. Academia — 1h20

Carga estimada: 3h50
```

## 20. Revisão diária evoluída

Na primeira utilização do dia:

```text
Bom dia.

Planejamento sugerido para hoje:
5 atividades
3h20 estimadas

[ Revisar plano ] [ Tudo certo ]
```

Ao revisar:

- mostrar ordem;
- explicar prioridade;
- permitir drag-and-drop;
- permitir retirar do dia;
- permitir reagendar.

## 21. Autoridade manual

Se o usuário reordenar ou reagendar algo manualmente, preservar essa decisão no plano atual. O motor não deve desfazê-la imediatamente.

## 22. Tudo concluído

Quando todas as tarefas do dia forem concluídas:

```text
✓ Tudo concluído por hoje.
```

Não puxar automaticamente tarefas de amanhã. Pode haver `Ver amanhã`, mas nunca geração silenciosa de trabalho extra.

## 23. Score interno

Pode existir um score interno, desde que:

- determinístico;
- centralizado;
- testável;
- documentado;
- não exposto como verdade absoluta.

Evitar números mágicos espalhados pelo código.

## 24. Horizonte

Planejar um horizonte curto, por exemplo 7–14 dias. Não recalcular meses inteiros a cada execução.

## 25. Conflitos

Se a carga não couber antes dos prazos:

```text
Não foi possível acomodar todo o trabalho antes dos prazos.
Carga necessária: 11h
Capacidade disponível: 7h
```

Não fingir que o plano é viável.

## 26. Arquitetura

Criar serviços dedicados, por exemplo:

```text
PlanningEngine
PriorityEngine
CapacityService
PlanningExplanationService
```

O motor deve receber dados puros e retornar um plano, sem depender diretamente da UI.

Exemplo conceitual:

```text
PlanningInput -> PlanningResult
```

## 27. Banco

Adicionar campos/tabelas para:

```text
Priority
EstimatedMinutes
CanSplit
WeeklyCapacity
DateCapacityOverride
ManualPlanningFlags
PlanningMetadata
```

Use migração incremental e não destrutiva.

## 28. Critérios de aceite

- [ ] V0.6 continua funcional.
- [ ] prioridade manual funciona.
- [ ] capacidade semanal funciona.
- [ ] exceção por data funciona.
- [ ] dia 0h não recebe item automático.
- [ ] item divisível pode ser quebrado.
- [ ] item não divisível não é quebrado.
- [ ] prazos influenciam prioridade.
- [ ] provas influenciam prioridade.
- [ ] domínio fraco influencia estudo.
- [ ] margem antes da prova é preservada quando possível.
- [ ] sobrecarga é detectada.
- [ ] redistribuição funciona.
- [ ] replanejamento pede confirmação.
- [ ] explicações são exibidas.
- [ ] drag manual é preservado.
- [ ] terminar tudo não gera trabalho extra.
- [ ] conflitos são informados claramente.
- [ ] migração não perde dados.

## 29. Testes mínimos

Testar prazo amanhã, prazo distante, prioridade alta/baixa, atraso no planejamento, prova próxima, domínio fraco, capacidade 0, exceção por data, divisibilidade, margem de 1 dia, sobrecarga, replanejamento, preservação manual, conflito sem solução, horizonte e migração V0.6→V0.7.

## 30. Definition of Done

A V0.7 está pronta quando compila, mantém a V0.6 íntegra, o motor é determinístico e explicável, respeita capacidade e margens, detecta sobrecarga, replaneja apenas com confirmação, preserva controle manual, testes passam e README documenta o motor.

**Não substituir o julgamento do usuário por decisões silenciosas do algoritmo.**
