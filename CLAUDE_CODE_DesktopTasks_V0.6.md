# CLAUDE CODE — DESKTOPTASKS V0.6
## Provas e planejamento estruturado de estudo

> Base obrigatória: partir da V0.5 funcional. Preserve os recursos anteriores. Prova deve ser entidade própria. O motor global fica para a V0.7.

## 1. Objetivo

Tratar provas como estruturas próprias com disciplina, data, conteúdos, listas de exercícios, nível de domínio, sessões de estudo e duração estimada.

## 2. Escopo congelado

Implementar:

1. `Exam`;
2. conteúdos da prova;
3. níveis `Não estudado / Fraco / Razoável / Bom`;
4. listas de exercícios;
5. sessões de estudo;
6. duração estimada;
7. integração com Hoje, Semana e Calendário;
8. geração assistida de plano de estudo;
9. uso do distribuidor simples da V0.5;
10. margem antes da prova;
11. progresso objetivo;
12. migração segura.

## 3. Fora do escopo

Não implementar IA, repetição espaçada avançada, previsão de nota, score mágico de preparação, redistribuição global silenciosa ou recursos da V0.7.

## 4. Entidade Prova

Exemplo:

```text
PROVA — MPP-31
Data: 10/09/2026
```

Campos sugeridos:

```text
Id
Subject
Title
ExamDate
Notes nullable
CreatedAt
```

## 5. Conteúdos

Permitir cadastrar conteúdos e listas, por exemplo:

```text
Lista 1
Lista 2
Lista 3
Teoria X
Capítulo Y
```

Cada conteúdo possui domínio:

```text
Não estudado
Fraco
Razoável
Bom
```

## 6. Listas de exercícios

Exemplo:

```text
Lista 1
12 questões
Estimativa: 1h30
```

Campos opcionais: `QuestionCount`, `EstimatedMinutes`.

## 7. Sessões de estudo

Criar estrutura de sessão com:

```text
ExamId
Topic/ListId nullable
Title
EstimatedMinutes
PlannedDate
CanSplit
IsCompleted
CompletedAt nullable
```

Sessões entram no mesmo ecossistema de Hoje/Semana.

## 8. Exemplo principal

Entrada:

```text
Prova: MPP-31
Data: 10/09

- estudar + fazer Lista 1
- estudar + fazer Lista 2
- estudar + fazer Lista 3
```

Plano sugerido:

```text
06/09 — Lista 1
07/09 — Lista 2
08/09 — Lista 3
09/09 — revisão/margem
10/09 — prova
```

## 9. Margem antes da prova

Como provas podem ocorrer de manhã, o sistema deve tentar concluir o conteúdo principal antes da véspera.

Regra preferencial:

- terminar conteúdo até pelo menos 1 dia antes;
- usar o dia anterior para revisão/margem/descanso;
- se o intervalo for insuficiente, usar os dias necessários e informar conflito quando aplicável.

## 10. Gerar plano de estudo

Adicionar ação:

```text
Gerar plano de estudo
```

O plano deve considerar:

1. data da prova;
2. número de blocos;
3. duração estimada;
4. dias com capacidade 0;
5. tendência a antecipar;
6. margem anterior à prova;
7. liberdade de edição manual.

Mostrar proposta antes de aplicar.

## 11. Progresso

Mostrar somente indicadores objetivos, por exemplo:

```text
Conteúdos estudados: 3/5
Listas concluídas: 2/4
```

Pode haver barra por quantidade. Não criar percentual artificial baseado em pesos obscuros.

## 12. Integração

Hoje:

```text
☐ MPP-31 — estudar Lista 1
  1h30 · prova em 4 dias
```

Semana: sessões nos dias planejados.

Calendário: mostrar data da prova e sessões planejadas, distinguindo prova de tarefa comum.

## 13. Atualização de domínio

Permitir troca rápida entre os quatro níveis de domínio.

## 14. Alteração do conteúdo

Permitir adicionar, editar, remover/desativar conteúdos e listas. Se houver sessões futuras dependentes, confirmar impacto antes de removê-las.

## 15. Banco

Adicionar estruturas como:

```text
Exams
ExamTopics
ExamExerciseLists
StudySessions
```

Use migração incremental e preserve tudo da V0.5.

## 16. Critérios de aceite

- [ ] V0.5 continua funcional.
- [ ] Prova pode ser criada e editada.
- [ ] Conteúdos e níveis de domínio funcionam.
- [ ] Listas funcionam.
- [ ] Sessões funcionam.
- [ ] Duração estimada funciona.
- [ ] Plano de estudo pode ser gerado.
- [ ] Margem anterior à prova é priorizada.
- [ ] Usuário pode alterar o plano.
- [ ] Sessões aparecem em Hoje e Semana.
- [ ] Prova aparece no Calendário.
- [ ] Progresso objetivo funciona.
- [ ] Migração não perde dados.

## 17. Testes mínimos

Testar criação de prova, conteúdos, domínio, listas, sessões, distribuição, margem, prova muito próxima, dias indisponíveis, divisibilidade, calendário e migração V0.5→V0.6.

## 18. Definition of Done

A V0.6 está pronta quando compila, mantém a V0.5 íntegra, Provas funcionam, sessões e plano assistido funcionam, o usuário permanece no controle, testes passam e README está atualizado.

**Não implementar o motor global da V0.7 aqui.**
