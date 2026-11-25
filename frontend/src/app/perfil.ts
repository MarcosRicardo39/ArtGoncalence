import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ContaApi, Perfil, mensagemDeErro } from './conta-api';

@Component({
  imports: [ReactiveFormsModule, RouterLink],
  selector: 'app-perfil',
  templateUrl: './perfil.html',
})
export class PerfilArtista {
  private readonly api = inject(ContaApi);
  private readonly rota = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private carregamento = 0;

  readonly editavel = this.rota.snapshot.data['editavel'] === true;
  readonly sessao = this.api.sessao;
  readonly perfil = signal<Perfil | null>(null);
  readonly buscando = signal(true);
  readonly salvando = signal(false);
  readonly erro = signal('');
  readonly aviso = signal('');
  readonly salvandoApoio = signal(false);
  readonly erroApoio = signal('');
  readonly avisoApoio = signal('');
  readonly linkApoio = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/\S/), Validators.maxLength(256)],
  });
  readonly formulario = new FormGroup({
    nomePublico: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200), Validators.pattern(/\S/)],
    }),
    bio: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
  });

  constructor() {
    this.rota.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      void this.carregar(params.get('id'));
    });
  }

  async salvar(): Promise<void> {
    this.formulario.markAllAsTouched();
    if (!this.editavel || this.formulario.invalid || this.salvando()) return;

    this.erro.set('');
    this.aviso.set('');
    this.salvando.set(true);
    const { nomePublico, bio } = this.formulario.getRawValue();
    try {
      await this.api.editarPerfil(nomePublico.trim(), bio.trim() || null);
      const perfil = this.perfil();
      if (perfil) {
        this.perfil.set({ ...perfil, nomePublico: nomePublico.trim(), bio: bio.trim() || null });
      }
      this.aviso.set('Perfil atualizado.');
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        await this.router.navigateByUrl('/conta/entrar');
        return;
      }
      this.erro.set(mensagemDeErro(error, 'Não foi possível salvar o perfil. Tente novamente.'));
    } finally {
      this.salvando.set(false);
    }
  }

  iniciais(nome: string): string {
    const palavras = nome.trim().split(/\s+/u);
    const selecionadas =
      palavras.length > 1 ? [palavras[0], palavras[palavras.length - 1]] : palavras;
    return selecionadas
      .map((palavra) => Array.from(palavra)[0] ?? '')
      .join('')
      .toLocaleUpperCase('pt-BR');
  }

  async salvarApoio(remover = false): Promise<void> {
    if (!this.editavel || this.salvandoApoio()) return;
    const perfil = this.perfil();
    if (!perfil) return;
    if (!remover) {
      this.linkApoio.markAsTouched();
      if (this.linkApoio.invalid) return;
    }

    this.salvandoApoio.set(true);
    this.erroApoio.set('');
    this.avisoApoio.set('');
    try {
      await this.api.definirApoio(remover ? null : this.linkApoio.value.trim());
      const atualizado = await this.api.perfil(perfil.id);
      this.perfil.update((atual) => (atual ? { ...atual, apoio: atualizado.apoio } : atual));
      this.linkApoio.reset(atualizado.apoio?.url ?? '');
      this.avisoApoio.set(remover ? 'Link removido.' : 'Link de apoio atualizado.');
    } catch (erro) {
      if (erro instanceof HttpErrorResponse && erro.status === 401) {
        await this.router.navigateByUrl('/conta/entrar');
        return;
      }
      this.erroApoio.set(
        erro instanceof HttpErrorResponse && erro.status === 400
          ? 'Use um link direto como https://ko-fi.com/seuperfil.'
          : mensagemDeErro(erro, 'Não foi possível atualizar o link de apoio.'),
      );
    } finally {
      this.salvandoApoio.set(false);
    }
  }

  private async carregar(id: string | null): Promise<void> {
    const tentativa = ++this.carregamento;
    this.buscando.set(true);
    this.perfil.set(null);
    this.erro.set('');
    try {
      if (this.editavel) {
        const sessao = await this.api.atualizarSessao();
        if (!sessao) {
          await this.router.navigateByUrl('/conta/entrar');
          return;
        }
        id = sessao.id;
      }

      if (!id) {
        this.erro.set('Perfil de artista indisponível.');
        return;
      }

      const perfil = await this.api.perfil(id);
      if (tentativa !== this.carregamento) return;
      this.perfil.set(perfil);
      this.formulario.reset({ nomePublico: perfil.nomePublico, bio: perfil.bio ?? '' });
      this.linkApoio.reset(perfil.apoio?.url ?? '');
    } catch (error) {
      if (tentativa === this.carregamento) {
        this.erro.set(mensagemDeErro(error, 'Perfil de artista indisponível.'));
      }
    } finally {
      if (tentativa === this.carregamento) this.buscando.set(false);
    }
  }
}
