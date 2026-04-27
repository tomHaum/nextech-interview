export interface Story {
  id: number;
  title: string;
  url: string | null;
  by: string;
  time: number;
  score: number;
}

export interface StoriesResponse {
  items: Story[];
  total: number;
  page: number;
  pageSize: number;
}
