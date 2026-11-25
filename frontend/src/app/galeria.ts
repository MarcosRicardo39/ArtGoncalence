import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ContaApi, mensagemDeErro } from './conta-api';
import { GaleriaApi, GaleriaPublica, IlustracaoPublica } from './galeria-api';

@Component({
  imports: [RouterLink],
  templateUrl: './galeria.html',
})
export class Galeria {
  private readonly api = inject(GaleriaApi);
  private readonly rota = inject(ActivatedRoute);
  private tentativa = 0;
  readonly sessao = inject(ContaApi).sessao;
  readonly dados = signal<GaleriaPublica | null>(null);
  readonly buscando = signal(true);
  readonly erro = signal('');
  readonly pagina = signal(1);
  readonly paginas = computed(() => {
    const dados = this.dados();
    return dados ? Math.max(1, Math.ceil(dados.total / dados.pageSize)) : 1;
  });

  constructor() {
    this.rota.queryParamMap.pipe(takeUntilDestroyed()).subscribe((parametros) => {
      const pagina = Number(parametros.get('page') ?? 1);
      this.pagina.set(Number.isInteger(pagina) && pagina > 0 && pagina <= 2147483647 ? pagina : 1);
      void this.carregar();
    });
  }

  async carregar(): Promise<void> {
    const tentativa = ++this.tentativa;
    this.buscando.set(true);
    this.erro.set('');
    this.dados.set(null);
    try {
      const dados = await this.api.listar(this.pagina(), 12);
      if (tentativa === this.tentativa) this.dados.set(dados);
    } catch (error) {
      if (tentativa === this.tentativa) {
        this.erro.set(mensagemDeErro(error, 'Não foi possível carregar a galeria.'));
      }
    } finally {
      if (tentativa === this.tentativa) this.buscando.set(false);
    }
  }
}

@Component({
  imports: [RouterLink, FormsModule],
  templateUrl: './ilustracao.html',
})
export class Ilustracao {
  private readonly api = inject(GaleriaApi);
  private readonly conta = inject(ContaApi);
  readonly sessao = this.conta.sessao;
  readonly item = signal<IlustracaoPublica | null>(null);
  readonly buscando = signal(true);
  readonly erro = signal('');
  readonly denunciaAberta = signal(false);
  readonly denunciaEnviando = signal(false);
  readonly denunciaEnviada = signal(false);
  readonly erroDenuncia = signal('');
  motivo = 'OUTRO';
  descricao = '';
  contato = '';

  async denunciar(id: string): Promise<void> {
    if (this.denunciaEnviando() || this.denunciaEnviada() || !this.descricao.trim()) return;
    this.denunciaEnviando.set(true);
    this.erroDenuncia.set('');
    try {
      await this.conta.mutarComCsrf(
        'POST',
        `/api/ilustracoes/${encodeURIComponent(id)}/denuncias`,
        {
          motivo: this.motivo,
          descricao: this.descricao.trim(),
          contato: this.contato.trim() || null,
        },
      );
      if (this.item()?.id !== id) return;
      this.descricao = '';
      this.contato = '';
      this.denunciaEnviada.set(true);
    } catch (error) {
      if (this.item()?.id === id) {
        this.erroDenuncia.set(
          mensagemDeErro(
            error,
            'Não foi possível enviar a denúncia. Confira os campos e tente novamente.',
          ),
        );
      }
    } finally {
      this.denunciaEnviando.set(false);
    }
  }

  constructor() {
    const rota = inject(ActivatedRoute);
    const destroyRef = inject(DestroyRef);
    let tentativa = 0;
    rota.paramMap.pipe(takeUntilDestroyed(destroyRef)).subscribe((parametros) => {
      const id = parametros.get('id') ?? '';
      this.denunciaAberta.set(false);
      this.denunciaEnviada.set(false);
      this.erroDenuncia.set('');
      this.motivo = 'OUTRO';
      this.descricao = '';
      this.contato = '';
      const ativa = ++tentativa;
      this.buscando.set(true);
      this.item.set(null);
      this.erro.set('');
      void this.api
        .buscar(id)
        .then(
          (item) => {
            if (ativa === tentativa) this.item.set(item);
          },
          (error) => {
            if (ativa === tentativa)
              this.erro.set(mensagemDeErro(error, 'Ilustração indisponível.'));
          },
        )
        .finally(() => {
          if (ativa === tentativa) this.buscando.set(false);
        });
    });
  }
}
