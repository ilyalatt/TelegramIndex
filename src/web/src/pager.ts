import { bindable } from "aurelia-framework";

export class Pager {
  readonly sideCellsCount = 3;
  @bindable selectedPage = 1;
  @bindable pagesCount = 1;
  showFirstPage = false;
  showLastPage = false;
  leftCells: number[] = [];
  rightCells: number[] = [];

  private range(n: number) {
    return Array.from(Array(n).keys());
  }

  private rangeFrom(start: number, n: number) {
    return this.range(n).map(x => x + start);
  }

  updateUi() {
    this.showFirstPage = this.selectedPage - this.sideCellsCount > 1;
    this.showLastPage = this.selectedPage + this.sideCellsCount < this.pagesCount;

    let leftCellsStart = Math.max(1, this.selectedPage - this.sideCellsCount + (this.showFirstPage ? 1 : 0));
    this.leftCells = this.rangeFrom(leftCellsStart, this.selectedPage - leftCellsStart);

    let rightCellsEnd = Math.min(this.pagesCount, this.selectedPage + this.sideCellsCount - (this.showLastPage ? 1 : 0));
    this.rightCells = this.rangeFrom(this.selectedPage + 1, rightCellsEnd - this.selectedPage);
  }

  private _ignoreSelectedPageChange = false;
  selectedPageChanged() {
    if (this._ignoreSelectedPageChange) return;

    this._ignoreSelectedPageChange = true;
    this.selectedPage = Math.max(1, Math.min(this.pagesCount, this.selectedPage));
    this._ignoreSelectedPageChange = false;

    this.updateUi();
  };

  selectPage(page: number) {
    this.selectedPage = page
  }

  private _ignorePagesCountChange = false;
  pagesCountChanged() {
    if (this._ignorePagesCountChange) return;

    this._ignorePagesCountChange = true;
    this.pagesCount = Math.max(1, this.pagesCount)
    this._ignorePagesCountChange = false;

    this.selectedPageChanged();
    this.updateUi();
  }
}
