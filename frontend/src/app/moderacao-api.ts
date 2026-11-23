import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ContaApi } from './conta-api';

export interface Pagina<T> {
  page: number;
  pageSize: number;
  total: number;
  itens: T[];
}

export interface ObraEmRevisao {
  id: string;
  titulo: string;
  descricao: string;
  categoria: 'ORIGINAL' | 'FANART';
  imagemUrl: string;
  artista?: string;
}

export interface DenunciaRecebida {
  id: string;
  ilustracaoId: string;
  motivo: 'DIREITOS_AUTORAIS' | 'OUTRO';
  descricao: string;
  contato: string | null;
  criadoEm: string;
}

export type AcaoModeracao = 'APROVAR' | 'REJEITAR' | 'OCULTAR';

@Injectable({ providedIn: 'root' })
export class ModeracaoApi {
  private readonly http = inject(HttpClient);
  private readonly conta = inject(ContaApi);
  private acesso?: { id: string; resultado: Promise<boolean> };

  podeAcessar(id: string): Promise<boolean> {
    if (this.acesso?.id === id) return this.acesso.resultado;
    const resultado = this.pendentes(1, 1).then(
      () => true,
      (error) => {
        if (!(error instanceof HttpErrorResponse) || ![401, 403].includes(error.status)) {
          if (this.acesso?.id === id) this.acesso = undefined;
        }
        return false;
      },
    );
    this.acesso = { id, resultado };
    return resultado;
  }

  limparAcesso(): void {
    this.acesso = undefined;
  }

  pendentes(page: number, pageSize: number): Promise<Pagina<ObraEmRevisao>> {
    return this.listar<ObraEmRevisao>('pendentes', page, pageSize);
  }

  denuncias(page: number, pageSize: number): Promise<Pagina<DenunciaRecebida>> {
    return this.listar<DenunciaRecebida>('denuncias', page, pageSize);
  }

  decidir(id: string, acao: AcaoModeracao, justificativa: string): Promise<void> {
    return this.conta.mutarComCsrf('POST', `/api/moderacao/${encodeURIComponent(id)}/decisao`, {
      acao,
      justificativa,
    });
  }

  encerrar(id: string, justificativa: string): Promise<void> {
    return this.conta.mutarComCsrf(
      'POST',
      `/api/moderacao/denuncias/${encodeURIComponent(id)}/encerrar`,
      {
        justificativa,
      },
    );
  }

  private listar<T>(recurso: string, page: number, pageSize: number): Promise<Pagina<T>> {
    return firstValueFrom(
      this.http.get<Pagina<T>>(`/api/moderacao/${recurso}`, { params: { page, pageSize } }),
    );
  }
}
