import React from 'react';
import { Box, Typography, Chip } from '@mui/material';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { vscDarkPlus } from 'react-syntax-highlighter/dist/esm/styles/prism';
import CodeIcon from '@mui/icons-material/Code';

/**
 * Renders one or more SQL query strings in a dark syntax-highlighted code block.
 *
 * Props:
 *  sqls  — string | string[]  — one or multiple SQL queries
 *  label — optional heading label
 */
const SqlCodeBlock = ({ sqls, label }) => {
  const sqlList = Array.isArray(sqls) ? sqls : sqls ? [sqls] : [];

  if (sqlList.length === 0) {
    return (
      <Box
        sx={{
          border: '1px dashed #ccc',
          borderRadius: 2,
          p: 2,
          bgcolor: '#FAFAFA',
          textAlign: 'center',
        }}
      >
        <Typography variant="caption" color="text.secondary">
          No SQL query available for this report.
        </Typography>
      </Box>
    );
  }

  return (
    <Box>
      {label && (
        <Box display="flex" alignItems="center" gap={1} mb={1}>
          <CodeIcon fontSize="small" color="primary" />
          <Typography variant="subtitle2" fontWeight={600}>
            {label}
          </Typography>
          <Chip label={`${sqlList.length} quer${sqlList.length > 1 ? 'ies' : 'y'}`} size="small" />
        </Box>
      )}
      {sqlList.map((sql, idx) => (
        <Box key={idx} sx={{ mb: idx < sqlList.length - 1 ? 2 : 0, borderRadius: 2, overflow: 'hidden' }}>
          {sqlList.length > 1 && (
            <Box sx={{ bgcolor: '#2d2d2d', px: 2, py: 0.75 }}>
              <Typography variant="caption" sx={{ color: '#858585', fontFamily: 'monospace' }}>
                -- Query {idx + 1}
              </Typography>
            </Box>
          )}
          <SyntaxHighlighter
            language="sql"
            style={vscDarkPlus}
            customStyle={{
              margin: 0,
              borderRadius: sqlList.length > 1 && idx < sqlList.length - 1 ? '0 0 8px 8px' : '8px',
              fontSize: '0.8rem',
              maxHeight: '320px',
              overflowY: 'auto',
            }}
            showLineNumbers
            wrapLongLines={false}
          >
            {sql.trim()}
          </SyntaxHighlighter>
        </Box>
      ))}
    </Box>
  );
};

export default SqlCodeBlock;
