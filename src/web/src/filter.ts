import { observable } from 'aurelia-binding';
import { Message } from './view-models';

export class Filter {
  filteredMessages: Message[] = [];
  @observable searchStr = '';
  readonly pageSize = 20;
  @observable selectedPage = 1;
  pagesCount = 0;

  private _isProcessingFilter = false;
  private _lastSearchResult: Message[] = [];
  private _lastSearchResultSearchStr: string = '';

  constructor(private readonly _messages: Message[]) {
    this._lastSearchResult = _messages;
    this.filter();
  }

  private syncWithLastSearchResult() {
    let msgs = this._lastSearchResult;
    this.pagesCount = 1 + Math.trunc((msgs.length - 1) / this.pageSize);
    this.filteredMessages = msgs.slice((this.selectedPage - 1) * this.pageSize).slice(0, this.pageSize);
  }

  filter() {
    if (this._isProcessingFilter) return;
    this._isProcessingFilter = true;

    try {
      // need to preserve tokens like c++, c#
      let punctuation = ['.', ',', '?', '!'];
      let tokens = punctuation.reduce((a, x) => a.replace(x, ' '), this.searchStr).split(' ').map(x => x.toLowerCase());
      let msgs = this.searchStr.startsWith(this._lastSearchResultSearchStr)
        ? this._lastSearchResult
        : this._messages;
      this._lastSearchResultSearchStr = this.searchStr;
      this._lastSearchResult = msgs.filter(x =>
        tokens.every(t =>
          [x.text, x.user.firstName, x.user.lastName, x.user.username]
          .filter(x => x != null)
          .map(x => x!.toLowerCase())
          .find(str => str.includes(t)) != null
        )
      );

      this.syncWithLastSearchResult();
    }
    finally {
      this._isProcessingFilter = false;
    }
  }

  private _ignoreSelectedPageChange = false;
  private selectedPageChanged() {
    if (this._ignoreSelectedPageChange) return;
    window.scrollTo(0, 0);

    this.syncWithLastSearchResult();
  }

  private searchStrChanged() {
    this._ignoreSelectedPageChange = true;
    this.selectedPage = 1;
    this._ignoreSelectedPageChange = false;

    this.filter();
  }
}
