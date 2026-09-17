# CLAUDE CODE — DESKTOPTASKS V0.5
## Projetos, Entregas, subtarefas e distribuição simples

> Base obrigatória: partir da V0.4 funcional. Preserve tudo o que já existe e implemente apenas o delta desta versão. Não reescreva o projeto do zero e não antecipe o motor global da V0.7.

## 1. Objetivo

Adicionar dois conceitos distintos:

- **Projeto**: iniciativa maior, com várias etapas e acompanhamento ao longo do tempo.
- **Entrega**: item de escopo menor, com poucos passos rápidos e resultado final definido.

Projeto e Entrega podem compartilhar infraestrutura interna, mas devem permanecer conceitos separados na interface.

## 2. Escopo congelado

Implementar:

1. `Project`;
2. `Delivery`;
3. apenas um nível de subtarefas;
4. criação, edição e exclusão;
5. subtarefas com `PlannedDate`, `DueDate`, `EstimatedMinutes` e `CanSplit`;
6. progresso por quantidade concluída;
7. progresso contextual em Hoje, Semana, Projeto e Entrega;
8. mover tarefa existente para Projeto/Entrega;
9. confirmação ao concluir a última subtarefa;
10. distribuição automática simples das subtarefas antes do prazo;
11. margem de segurança antes do prazo quando possível;
12. edição manual do plano sugerido;
13. migração segura do banco.

## 3. Fora do escopo

Não implementar sub-subtarefas, árvore infinita, Gantt, dependências complexas, IA, planejamento global de toda a agenda, redistribuição silenciosa ou recursos da V0.6/V0.7.

## 4. Estruturas conceituais

Exemplo de Projeto:

```text
PROJETO
Preparação para mestrado

☐ Pesquisar programas
☐ Escolher orientadores
☐ Atualizar currículo
☐ Ler editais
☐ Preparar proposta
```

Exemplo de Entrega:

```text
ENTREGA
Relatório de laboratório
Prazo: 15/09

☐ Organizar dados
☐ Fazer gráficos
☐ Escrever conclusão
☐ Revisar PDF
```

## 5. Subtarefas

Somente um nível:

```text
Projeto/Entrega
  ├── Subtarefa 1
  ├── Subtarefa 2
  └── Subtarefa 3
```

Campos mínimos:

```text
Id
ParentId
Title
PlannedDate nullable
DueDate nullable
EstimatedMinutes nullable
CanSplit
IsCompleted
CompletedAt nullable
SortOrder
```

## 6. Progresso

Mostrar:

```text
3 de 7 concluídas
████░░░░░░ 43%
```

Usar a regra simples `concluídas / total`. Não ponderar por duração ainda.

Exibir progresso também nas abas Hoje e Semana.

## 7. Conclusão do todo

Ao concluir a última subtarefa, mostrar:

```text
Todas as etapas foram concluídas.

O projeto "X" está concluído?

[ Sim, concluir ] [ Ainda não ]
```

Mesmo comportamento para Entrega. Não concluir automaticamente.

## 8. Mover tarefa existente

Permitir associar uma tarefa existente a Projeto ou Entrega preservando texto, conclusão, datas, duração e ordem sempre que aplicável.

## 9. Distribuição automática simples

Adicionar ação:

```text
Distribuir automaticamente
```

Entradas:

- data final (`DueDate`) do Projeto/Entrega;
- lista de subtarefas;
- duração estimada quando conhecida;
- disponibilidade diária quando já existir/configurada.

Saída: proposta de `PlannedDate` para cada subtarefa.

## 10. Regra temporal

O distribuidor deve:

1. trabalhar do dia atual até o prazo;
2. evitar concentrar tudo na véspera;
3. distribuir ao longo do período com tendência moderada a antecipar;
4. tentar reservar **1 dia anterior ao prazo** como margem de segurança;
5. não usar dias com capacidade 0;
6. permitir edição manual antes de aplicar.

Exemplo:

```text
Prazo: 10/09
06/09 — Etapa 1
07/09 — Etapa 2
08/09 — Etapa 3
09/09 — margem/revisão
10/09 — prazo
```

## 11. Tarefa maior que a capacidade

Se `CanSplit = true`, pode dividir em sessões.

Exemplo:

```text
4h totais
2h hoje
2h amanhã
```

Se `CanSplit = false`, procurar um dia com capacidade suficiente. Se nenhum existir, informar conflito; não quebrar silenciosamente.

## 12. Plano sugerido

Antes de salvar:

```text
PLANO SUGERIDO
06/09  Lista 1
07/09  Lista 2
08/09  Lista 3
09/09  Margem de segurança

[ Aceitar ] [ Ajustar ]
```

Nunca aplicar uma redistribuição automática silenciosamente.

## 13. Banco

Adicionar estruturas para Projetos, Entregas e suas subtarefas sem destruir `Tasks` nem dados anteriores. Use migrações incrementais.

## 14. Critérios de aceite

- [ ] V0.4 continua funcional.
- [ ] Projeto funciona.
- [ ] Entrega funciona.
- [ ] Só existe um nível de subtarefa.
- [ ] Subtarefas aceitam PlannedDate, DueDate, duração e CanSplit.
- [ ] Progresso funciona em Projeto, Entrega, Hoje e Semana.
- [ ] Tarefa existente pode ser movida para um todo maior.
- [ ] Última subtarefa abre confirmação.
- [ ] Distribuição gera plano plausível.
- [ ] Margem de 1 dia é tentada quando possível.
- [ ] Dia com capacidade 0 não recebe item automático.
- [ ] Usuário pode alterar o plano.
- [ ] Migração não perde dados.

## 15. Testes mínimos

Testar criação, progresso, conclusão, associação de tarefa, distribuição com prazo distante/próximo, margem, dia indisponível, tarefa divisível, tarefa não divisível e migração V0.4→V0.5.

## 16. Definition of Done

A V0.5 está pronta quando compila, mantém a V0.4 íntegra, Projeto e Entrega funcionam, o distribuidor simples funciona, o usuário mantém controle manual, testes passam e o README está atualizado.

**Não implementar o planejador global da V0.7 nesta versão.**
