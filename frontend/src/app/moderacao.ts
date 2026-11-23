import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { mensagemDeErro } from './conta-api';
import { GaleriaApi } from './galeria-api';
import { AcaoModeracao, DenunciaRecebida, ModeracaoApi, ObraEmRevisao } from './moderacao-api';

type Aba = 'pendentes' | 'aprovadas' | 'denuncias';
type Acao = AcaoModeracao | 'ENCERRAR';
type Decisao = { id: string; acao: Acao };

const ROTULOS: Record<Acao, string> = {
  APROVAR: 'Aprovar ilustração',
  REJEITAR: 'Rejeitar ilustração',
  OCULTAR: 'Ocultar ilustração',
  ENCERRAR: 'Encerrar denúncia',
};

@Component({
  imports: [DatePipe, NgTemplateOutlet, ReactiveFormsModule, RouterLink],
  templateUrl: './moderacao.html',
  styleUrl: './moderacao.css',
})
export class Moderacao {
  private readonly api = inject(ModeracaoApi);
  private readonly galeria = inject(GaleriaApi);
  private readonly router = inject(Router);
  private consulta = 0;
  private readonly porPagina = 10;

  readonly abas: ReadonlyArray<{ id: Aba; rotulo: string }> = [
    { id: 'pendentes', rotulo: 'Pendentes' },
    { id: 'aprovadas', rotulo: 'Aprovadas' },
    { id: 'denuncias', rotulo: 'Denúncias' },
  ];
  readonly aba = signal<Aba>('pendentes');
  readonly autorizado = signal(false);
  readonly restrito = signal(false);
  readonly buscando = signal(true);
  readonly enviando = signal(false);
  readonly erro = signal('');
  readonly aviso = signal('');
  readonly erroDecisao = signal('');
  readonly obras = signal<ObraEmRevisao[]>([]);
  readonly denuncias = signal<DenunciaRecebida[]>([]);
  readonly pagina = signal(1);
  readonly total = signal(0);
  readonly paginas = computed(() => Math.max(1, Math.ceil(this.total() / this.porPagina)));
  readonly decisao = signal<Decisao | null>(null);
  readonly rotulos = ROTULOS;
  readonly justificativa = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/\S/), Validators.maxLength(2000)],
  });

  constructor() {
    void this.carregar();
  }

  abrirAba(aba: Aba): void {
    if (this.enviando() || aba === this.aba()) return;
    this.aba.set(aba);
    this.pagina.set(1);
    this.aviso.set('');
    this.cancelar();
    void this.carregar();
  }

  mudarPagina(pagina: number): void {
    if (this.enviando() || pagina < 1 || pagina > this.paginas()) return;
    this.pagina.set(pagina);
    this.aviso.set('');
    this.cancelar();
    void this.carregar();
  }

  escolher(id: string, acao: Acao): void {
    if (this.enviando()) return;
    this.justificativa.reset();
    this.erroDecisao.set('');
    this.aviso.set('');
    this.decisao.set({ id, acao });
  }

  cancelar(): void {
    if (this.enviando()) return;
    this.decisao.set(null);
    this.erroDecisao.set('');
  }

  async carregar(): Promise<void> {
    const atual = ++this.consulta;
    const aba = this.aba();
    this.buscando.set(true);
    this.erro.set('');
    this.restrito.set(false);
    this.obras.set([]);
    this.denuncias.set([]);
    try {
      if (aba === 'denuncias') {
        const fila = await this.api.denuncias(this.pagina(), this.porPagina);
        if (atual !== this.consulta) return;
        this.denuncias.set(fila.itens);
        this.total.set(fila.total);
      } else if (aba === 'aprovadas') {
        await this.api.pendentes(1, 1);
        const fila = await this.galeria.listar(this.pagina(), this.porPagina);
        if (atual !== this.consulta) return;
        this.obras.set(
          fila.itens.map((item) => ({
            id: item.id,
            titulo: item.titulo,
            descricao: item.descricao,
            categoria: item.categoria,
            artista: item.artista.nomePublico,
            imagemUrl: `/api/moderacao/${encodeURIComponent(item.id)}/imagem`,
          })),
        );
        this.total.set(fila.total);
      } else {
        const fila = await this.api.pendentes(this.pagina(), this.porPagina);
        if (atual !== this.consulta) return;
        this.autorizado.set(true);
        this.obras.set(fila.itens);
        this.total.set(fila.total);
      }

      if (this.pagina() > this.paginas()) {
        this.pagina.set(this.paginas());
        await this.carregar();
      }
    } catch (error) {
      if (atual === this.consulta && !(await this.tratarAcesso(error))) {
        this.erro.set(mensagemDeErro(error, 'Não foi possível carregar a fila.'));
      }
    } finally {
      if (atual === this.consulta) this.buscando.set(false);
    }
  }

  async confirmar(): Promise<void> {
    const decisao = this.decisao();
    this.justificativa.markAsTouched();
    if (!decisao || this.justificativa.invalid || this.enviando()) return;

    this.enviando.set(true);
    this.erroDecisao.set('');
    const justificativa = this.justificativa.value.trim();
    try {
      if (decisao.acao === 'ENCERRAR') {
        await this.api.encerrar(decisao.id, justificativa);
        this.aviso.set('Denúncia encerrada.');
      } else {
        await this.api.decidir(decisao.id, decisao.acao, justificativa);
        this.aviso.set('Decisão registrada.');
      }
      this.decisao.set(null);
      await this.carregar();
    } catch (error) {
      if (await this.tratarAcesso(error)) return;
      if (error instanceof HttpErrorResponse && [404, 409].includes(error.status)) {
        this.decisao.set(null);
        this.aviso.set('O item foi atualizado por outra revisão. A fila foi recarregada.');
        await this.carregar();
        return;
      }
      this.erroDecisao.set(mensagemDeErro(error, 'Não foi possível registrar a decisão.'));
    } finally {
      this.enviando.set(false);
    }
  }

  private async tratarAcesso(error: unknown): Promise<boolean> {
    if (!(error instanceof HttpErrorResponse)) return false;
    if (error.status === 401) {
      await this.router.navigateByUrl('/conta/entrar');
      return true;
    }
    if (error.status === 403) {
      this.autorizado.set(false);
      this.restrito.set(true);
      this.api.limparAcesso();
      this.erro.set('Esta área é exclusiva para moderadores e administradores.');
      return true;
    }
    return false;
  }
}
